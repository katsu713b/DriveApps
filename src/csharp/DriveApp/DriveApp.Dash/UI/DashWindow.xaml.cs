using DriveApp.Dash.PFC;
using Microsoft.Extensions.Options;
using PFC;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace DriveApp.Dash.UI;

/// <summary>
/// DashWindow.xaml の相互作用ロジック
/// </summary>
public partial class DashWindow : Window
{
    // UIスレッド上で動くタイマー
    private DispatcherTimer _timer;
    private DashWindowVM _dataContext;
    private PFCContext _pFCContext;
    private WarningProvider _warningProvider;
    private DashSettings _dashSettings;

    //private const int CautionWaterTemp = 95;
    //private const int CautionAirTemp = 70;
    //private const int CautionFuelTemp = 50;
    //private const int CautionKnock = 50;
    //private const int CautionRpm = 7250;
    //private const int WarnRpm = 7400;
    //private const double RpmMaxValue = 8000.0;
    //private const int ThrottleVoltageMinValue = 510;
    //private const int ThrottleVoltageMaxValue = 4400;
    // 1 kg/cm2 = 98.0665 kPa
    // 1 mmHg = 0.00131579 kg/cm2
    // kPa = kg/cm2 x 9.80665

    private Stopwatch _stopwatch = new Stopwatch();
    private TimeSpan _startFps = TimeSpan.Zero;
    private int _fps = 0;

    public DashWindow(PFCContext pFCContext, IOptionsMonitor<DashSettings> settings)
    {
        _pFCContext = pFCContext;
        _dashSettings = settings.CurrentValue;

        InitializeComponent();

        _dataContext = (DashWindowVM)this.DataContext;

        _timer = new DispatcherTimer(DispatcherPriority.Normal);
        _timer.Interval = TimeSpan.FromMilliseconds(20);
        _timer.Tick += _timer_Tick;

        Loaded += (sender, e) =>
        {
            _stopwatch.Start();
            _timer.Start();
        };
        Closing += (sender, e) =>
        {
            _timer.Stop();
        };

        _warningProvider = new WarningProvider(this, _dashSettings.Warnings);
        this.Closing += DashWindow_Closing;
    }

    private void DashWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        using (_pFCContext) { }

        Application.Current.Shutdown();
    }

    private long _lastReceivedTime = 0;

    private void _timer_Tick(object? sender, EventArgs e)
    {
        if ((_stopwatch.Elapsed - _startFps).TotalMilliseconds > 1000)
        {
            _dataContext.FPS = _fps.ToString();
            _fps = 0;
            _startFps = _stopwatch.Elapsed;
            _dataContext.UpdateNow();
        }

        _fps++;

        var data = _pFCContext.LatestAdvancedData;

        if (data == null) return;
        if (_lastReceivedTime == data.ReceivedUnixTimeMs) return;

        _lastReceivedTime = data.ReceivedUnixTimeMs;

        //if (IsActive)
        //{
        //    UpdateDataContext(data);
        //}
        UpdateDataContext(data);
    }

    private Dictionary<string, (double Ratio, double Lower, double Upper)> GearRatio = new Dictionary<string, (double Ratio, double Lower, double Upper)>
    {
        {"3", (1.391, 1.279,  1.5) },    // +- 8%
        {"2", (2.015, 1.81,   2.216) }, // +- 10%
        {"4", (1,     0.95,   1.05) },   // +- 5%
        {"5", (0.806, 0.7657,  0.8463) },   // +- 5%
        {"1", (3.483, 2.96, 4.0) } // +- 15%
    };

    string GetGear(int speed, int rpm, double tireDiameter = 0.6)
    {
        var g = "N";
        if (speed == 0)
            return g;

        foreach (var gear in GearRatio)
        {
            double ratio = (rpm * tireDiameter * Math.PI * 60) / (1000 * speed * 4.1);
            //var ratio = (data.Speed * 60 * 0.631 * 3.14) / (data.Rpm * gear.Value.Ratio);
            if (gear.Value.Lower <= ratio && ratio <= gear.Value.Upper)
                return gear.Key;
        }

        return g;
    }

    private void UpdateDataContext(AdvancedData data)
    {
        _warningProvider.UpdateWarns(data);

        _dataContext.KnockLevel = data.KnockLevel.ToString();

        var rpm = data.Rpm;
        ReadOnlySpan<char> rpmSpan = rpm.ToString();
        _dataContext.RpmTop = rpmSpan.Length > 2 ? new string(rpmSpan[..(rpmSpan.Length - 2)]) : string.Empty;
        _dataContext.RpmUnder = rpmSpan.Length > 2 ? new string(rpmSpan[(rpmSpan.Length - 2)..]).PadLeft(2, '0') : new string(rpmSpan).PadLeft(2, '0');

        /*
            bar1  0-5000
            0__1250__2500__3750__5000

            bar   5000-7000
            red   7250-7400
            flush 7400-
            
            5000__5500__6000__6500__7000
         * */
        /*
         * 慣らし中
            bar   0-3000
            red   3000-3300
            flush 3300-
            
            0__750__1500__2250__3000
         * */


        if (rpm <= 0)
        {
            _dataContext.RpmBarPos = 0;
            _dataContext.RpmBarWidth = 800;
        }
        else
        {
            var rate = Math.Max(0, 1 - rpm / _dashSettings.RpmMaxValue);
            var width = 800 * Math.Min(1, rate);
            _dataContext.RpmBarPos = 800 - width;
            _dataContext.RpmBarWidth = width;
        }

        // Throttle bar
        {
            // 510 - 4400(mV)
            var throttle = Math.Max(0, data.ThrottleSensorVoltage - _dashSettings.ThrottleVoltageMinValue);
            var rate = Math.Max(0, 1 - (double)throttle / (_dashSettings.ThrottleVoltageMaxValue - _dashSettings.ThrottleVoltageMinValue));
            var width = 320 * Math.Min(1, rate);

            _dataContext.ThrottleBarPos = 320 - width;
            _dataContext.ThrottleBarWidth = width;
        }

        // Boost
        {
            const int positiveBarWidth = 220;
            const int negativeBarWidth = 100;

            var boost = data.AirIPressure * 0.980665; // 100KPa
            
            // Boost Bar
            //   positive  0.0 - 1.3(= 1.0 - 2.3)
            //   negative -0.7 - 0.0(= 0.3 - 1.0)
            if (boost >= 1)
            {
                var tmp = Math.Max(0, boost - 1);
                var rate = Math.Max(0, 1 - tmp / 1.3);
                var width = positiveBarWidth * Math.Min(1, rate);
                _dataContext.BoostBarPositivePos = positiveBarWidth + negativeBarWidth - width;
                _dataContext.BoostBarPositiveWidth = width;

                _dataContext.BoostBarNegativeWidth = negativeBarWidth;
            }
            else
            {
                var tmp = Math.Max(0, boost - 0.3);
                var rate = Math.Min(1, tmp / 0.7f);

                var width = negativeBarWidth * Math.Min(1, rate);
                _dataContext.BoostBarNegativeWidth = width;

                _dataContext.BoostBarPositivePos = negativeBarWidth;
                _dataContext.BoostBarPositiveWidth = positiveBarWidth;
            }

            _dataContext.Boost = (boost - 1).ToString("F2");
        }

        _dataContext.INJDuty = data.GetInjectorPrDuty().ToString("F1");

        _dataContext.Speed = data.Speed.ToString();

        _dataContext.FuelCorrection = data.FuelCorrection.ToString("F3");
        _dataContext.IGNAngleLd = data.IGNAngleLd.ToString();
        _dataContext.IGNAngleTr = data.IGNAngleTr.ToString();
        //_dataContext.MapSensorVoltage = data.MapSensorVoltage.ToString();
        //_dataContext.ThrottleSensorVoltage = (data.ThrottleSensorVoltage / 1000m).ToString();
        _dataContext.AirTemp = data.AirTemp.ToString();
        _dataContext.WaterTemp = data.WaterTemp.ToString();
        _dataContext.FuelTemp = data.FuelTemp.ToString();
        _dataContext.BattVoltage = data.BattVoltage.ToString("F1");


        if (_skipGearCount++ >= 3)
        {
            _dataContext.Gear = GetGear(data.Speed, data.Rpm);
            _skipGearCount = 0;
        }
        


        _dataContext.ConnectedPFC = _pFCContext.IsPFCConnected ? Visibility.Visible: Visibility.Hidden;
        _dataContext.ConnectedCMD = _pFCContext.IsCommanderConnected ? Visibility.Visible : Visibility.Hidden;

    }
    private int _skipGearCount = 0;

    private class WarningProvider
    {
        private DashSettingWarnings _warnings;

        private DashWindow _window;
        private Storyboard _mainWarn;
        private Storyboard _rpmWarn;

        private const string ColorAlert =   "#FF0000FF";
        private const string ColorCaution = "#FFFF0000";
        private const string ColorNormal = "#00000000";

        private static readonly TimeSpan _KnockMainWarnTime = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan _BoostCautionTime = TimeSpan.FromSeconds(3);
        private Stopwatch _warnTimer = new Stopwatch();

        // Alert(Blue) < Caution(Red) < Warn(Red Flush)
        public WarningProvider(DashWindow window, DashSettingWarnings warnings)
        {
            _warnings = warnings;
            _window = window;
            _mainWarn = window.Resources["sbMainWarn"] as Storyboard ?? throw new ArgumentNullException("sbMainWarn");
            _rpmWarn = window.Resources["sbRpmWarn"] as Storyboard ?? throw new ArgumentNullException("sbRpmWarn");
            _warnTimer.Start();
        }

        private TimeSpan _knockWarnStart = TimeSpan.MaxValue;
        private TimeSpan _boostCautionStart = TimeSpan.MaxValue;

        public void UpdateWarns(AdvancedData data)
        {
            var context = _window._dataContext;

            if (data.KnockLevel >= _warnings.CautionKnock)
            {
                BeginMainWarn();
                context.KnockBackGColor = ColorCaution;
                _knockWarnStart = _warnTimer.Elapsed;
            }
            else if ((_warnTimer.Elapsed - _knockWarnStart) >= _KnockMainWarnTime)
            {
                StopMainWarn();
                context.KnockBackGColor = ColorNormal;
            }

            var rpm = data.Rpm;
            if (rpm < _warnings.CautionRpm)
            {
                context.RpmBarCautionVisible = Visibility.Hidden;
                StopRpmWarn();
            }
            else if (rpm >= _warnings.CautionRpm && rpm < _warnings.WarnRpm)
            {
                context.RpmBarCautionVisible = Visibility.Visible;
                StopRpmWarn();
            }
            else if (rpm >= _warnings.WarnRpm)
            {
                context.RpmBarCautionVisible = Visibility.Hidden;
                BeginRpmWarn();
            }
            if ((data.AirIPressure - 1) >= 1.15)
            {
                context.BoostBackGColor = ColorCaution;
                _boostCautionStart = _warnTimer.Elapsed;
            }
            else if ((_warnTimer.Elapsed - _boostCautionStart) >= _BoostCautionTime)
            {
                context.BoostBackGColor = ColorNormal;
            }

            context.AirTempBackGColor = data.AirTemp >= _warnings.CautionAirTemp ? ColorCaution : ColorNormal;
            context.WaterTempBackGColor = data.WaterTemp >= _warnings.CautionWaterTemp ? ColorCaution : ColorNormal;
            context.FuelTempBackGColor = data.FuelTemp >= _warnings.CautionFuelTemp ? ColorCaution : ColorNormal;
        }

        private bool _beginingMainWarn = false;
        public void BeginMainWarn()
        {
            if (!_beginingMainWarn)
            {
                _window._dataContext.MainBackGColor = ColorNormal;
                _beginingMainWarn = true;
                _mainWarn.Begin(_window, true);
            }
        }

        public void StopMainWarn()
        {
            if (_beginingMainWarn)
            {
                _beginingMainWarn = false;
                _mainWarn.Stop(_window);
            }
        }

        private bool _beginingRpmWarn = false;
        private void BeginRpmWarn()
        {
            if (!_beginingRpmWarn)
            {
                _beginingRpmWarn = true;
                _rpmWarn.Begin(_window, true);
            }
        }
        private void StopRpmWarn()
        {
            if (_beginingRpmWarn)
            {
                _beginingRpmWarn = false;
                _rpmWarn.Stop(_window);
            }
        }
    }
}

public class DashWindowVM : ViewModelBase
{
    public void UpdateNow()
    {
        Now = DateTime.Now.ToString("HH:mm");
        NowSec = DateTime.Now.ToString("ss");
    }
    private string _Now = DateTime.Now.ToString("HH:mm");
    public string Now
    {
        get => _Now;
        set { if (_Now != value) { _Now = value; RaisePropertyChanged(); } }
    }
    private string _NowSec = DateTime.Now.ToString("ss");
    public string NowSec
    {
        get => _NowSec;
        set { if (_NowSec != value) { _NowSec = value; RaisePropertyChanged(); } }
    }

    private Visibility _ConnectedPFC = Visibility.Hidden;
    public Visibility ConnectedPFC
    {
        get => _ConnectedPFC;
        set { if (_ConnectedPFC != value) { _ConnectedPFC = value; RaisePropertyChanged(); } }
    }
    private Visibility _ConnectedCMD = Visibility.Hidden;
    public Visibility ConnectedCMD
    {
        get => _ConnectedCMD;
        set { if (_ConnectedCMD != value) { _ConnectedCMD = value; RaisePropertyChanged(); } }
    }

    private string _fps = "0";
    public string FPS
    {
        get => _fps;
        set { if (_fps != value) { _fps = value; RaisePropertyChanged(); } }
    }

    private string _MainBackGColor = _normalColor;
    public string MainBackGColor
    {
        get => _MainBackGColor;
        set { if (_MainBackGColor != value) { _MainBackGColor = value; RaisePropertyChanged(); } }
    }

    private string _Gear = "N";
    public string Gear
    {
        get => _Gear;
        set { if (_Gear != value) { _Gear = value; RaisePropertyChanged(); } }
    }

    #region Values

    private string _AirTemp = "0";
    public string AirTemp
    {
        get => _AirTemp;
        set { if (_AirTemp != value) { _AirTemp = value; RaisePropertyChanged(); } }
    }
    private string _BattVoltage = "0.0";
    public string BattVoltage
    {
        get => _BattVoltage;
        set { if (_BattVoltage != value) { _BattVoltage = value; RaisePropertyChanged(); } }
    }
    private string _Boost = "0.00";
    public string Boost
    {
        get => _Boost;
        set { if (_Boost != value) { _Boost = value; RaisePropertyChanged(); } }
    }
    private string _FuelCorrection = "0.00";
    public string FuelCorrection
    {
        get => _FuelCorrection;
        set { if (_FuelCorrection != value) { _FuelCorrection = value; RaisePropertyChanged(); } }
    }
    private string _FuelTemp = "0";
    public string FuelTemp
    {
        get => _FuelTemp;
        set { if (_FuelTemp != value) { _FuelTemp = value; RaisePropertyChanged(); } }
    }
    private string _IGNAngleLd = "0";
    public string IGNAngleLd
    {
        get => _IGNAngleLd;
        set { if (_IGNAngleLd != value) { _IGNAngleLd = value; RaisePropertyChanged(); } }
    }
    private string _IGNAngleTr = "0";
    public string IGNAngleTr
    {
        get => _IGNAngleTr;
        set { if (_IGNAngleTr != value) { _IGNAngleTr = value; RaisePropertyChanged(); } }
    }
    private string _KnockLevel = "0";
    public string KnockLevel
    {
        get => _KnockLevel;
        set { if (_KnockLevel != value) { _KnockLevel = value; RaisePropertyChanged(); } }
    }
    private string _MapSensorVoltage = string.Empty;
    public string MapSensorVoltage
    {
        get => _MapSensorVoltage;
        set { if (_MapSensorVoltage != value) { _MapSensorVoltage = value; RaisePropertyChanged(); } }
    }
    private string _RpmTop = "0";
    public string RpmTop
    {
        get => _RpmTop;
        set { if (_RpmTop != value) { _RpmTop = value; RaisePropertyChanged(); } }
    }
    private string _RpmUnder = "00";
    public string RpmUnder
    {
        get => _RpmUnder;
        set { if (_RpmUnder != value) { _RpmUnder = value; RaisePropertyChanged(); } }
    }
    private string _Speed = "0";
    public string Speed
    {
        get => _Speed;
        set { if (_Speed != value) { _Speed = value; RaisePropertyChanged(); } }
    }
    private string _ThrottleSensorVoltage = "0.0";
    public string ThrottleSensorVoltage
    {
        get => _ThrottleSensorVoltage;
        set { if (_ThrottleSensorVoltage != value) { _ThrottleSensorVoltage = value; RaisePropertyChanged(); } }
    }
    private string _WaterTemp = "0";
    public string WaterTemp
    {
        get => _WaterTemp;
        set { if (_WaterTemp != value) {_WaterTemp = value; RaisePropertyChanged(); }}
    }

    private string _INJDuty = "0.0";
    public string INJDuty
    {
        get => _INJDuty;
        set { if (_INJDuty != value) { _INJDuty = value; RaisePropertyChanged(); } }
    }
    #endregion

    #region Bar
    private double _RpmBarWidth = 0;
    public double RpmBarWidth
    {
        get => _RpmBarWidth;
        set { if (_RpmBarWidth != value) { _RpmBarWidth = value; RaisePropertyChanged(); } }
    }
    private double _RpmBarPos = 800;
    public double RpmBarPos
    {
        get => _RpmBarPos;
        set { if (_RpmBarPos != value) { _RpmBarPos = value; RaisePropertyChanged(); } }
    }
    private Visibility _RpmBarCautionVisible = Visibility.Hidden;
    public Visibility RpmBarCautionVisible
    {
        get => _RpmBarCautionVisible;
        set { if (_RpmBarCautionVisible != value) { _RpmBarCautionVisible = value; RaisePropertyChanged(); } }
    }
    private Visibility _RpmBarWarnVisible = Visibility.Hidden;
    public Visibility RpmBarWarnVisible
    {
        get => _RpmBarWarnVisible;
        set { if (_RpmBarWarnVisible != value) { _RpmBarWarnVisible = value; RaisePropertyChanged(); } }
    }
    private double _ThrottleBarWidth = 0;
    public double ThrottleBarWidth
    {
        get => _ThrottleBarWidth;
        set { if (_ThrottleBarWidth != value) { _ThrottleBarWidth = value; RaisePropertyChanged(); } }
    }
    private double _ThrottleBarPos = 0;
    public double ThrottleBarPos
    {
        get => _ThrottleBarPos;
        set { if (_ThrottleBarPos != value) { _ThrottleBarPos = value; RaisePropertyChanged(); } }
    }
    //    positive negative
    private double _BoostBarPositiveWidth = 0;
    public double BoostBarPositiveWidth
    {
        get => _BoostBarPositiveWidth;
        set { if (_BoostBarPositiveWidth != value) { _BoostBarPositiveWidth = value; RaisePropertyChanged(); } }
    }

    private double _BoostBarPositivePos = 320;
    public double BoostBarPositivePos
    {
        get => _BoostBarPositivePos;
        set { if (_BoostBarPositivePos != value) { _BoostBarPositivePos = value; RaisePropertyChanged(); } }
    }

    private double _BoostBarNegativeWidth = 100;
    public double BoostBarNegativeWidth
    {
        get => _BoostBarNegativeWidth;
        set { if (_BoostBarNegativeWidth != value) { _BoostBarNegativeWidth = value; RaisePropertyChanged(); } }
    }
    #endregion

    #region Values Alert/Caution/Warn

    private const string _normalColor = "#00000000";

    private string _BoostBackGColor = _normalColor;
    public string BoostBackGColor
    {
        get => _BoostBackGColor;
        set { if (_BoostBackGColor != value) { _BoostBackGColor = value; RaisePropertyChanged(); } }
    }
    private string _KnockBackGColor = _normalColor;
    public string KnockBackGColor
    {
        get => _KnockBackGColor;
        set { if (_KnockBackGColor != value) { _KnockBackGColor = value; RaisePropertyChanged(); } }
    }
    private string _WaterTempBackGColor = _normalColor;
    public string WaterTempBackGColor
    {
        get => _WaterTempBackGColor;
        set { if (_WaterTempBackGColor != value) { _WaterTempBackGColor = value; RaisePropertyChanged(); } }
    }
    private string _AirTempBackGColor = _normalColor;
    public string AirTempBackGColor
    {
        get => _AirTempBackGColor;
        set { if (_AirTempBackGColor != value) { _AirTempBackGColor = value; RaisePropertyChanged(); } }
    }
    private string _FuelTempBackGColor = _normalColor;
    public string FuelTempBackGColor
    {
        get => _FuelTempBackGColor;
        set { if (_FuelTempBackGColor != value) { _FuelTempBackGColor = value; RaisePropertyChanged(); } }
    }
    #endregion
}
