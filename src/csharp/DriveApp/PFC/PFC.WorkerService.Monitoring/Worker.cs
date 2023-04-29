using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.IO.Ports;
using static PFC.WorkerService.Monitoring.ConsoleAPI;

namespace PFC.WorkerService.Monitoring;

/// <summary>
/// PCFから読み取った情報をファイル出力する
/// 最新情報は共有メモリへ出力
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly PortOptions _portOptions;
    private SerialPort _serialPort;
    private Writer _writer;
    private Queue<AdvancedData> _queueDatas = new Queue<AdvancedData>();


    public Worker(ILogger<Worker> logger, IOptions<PortOptions> options, Writer writer)
    {
        _logger = logger;
        _portOptions = options.Value;
        _writer = writer;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // ここにサービス開始時の処理を書く
        _logger.LogInformation("start at: {time}", DateTimeOffset.Now);

        // コントロールハンドラ設定
        SetConsoleCtrlHandler(ControlHandler, true);

        var logpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!Directory.Exists(logpath)) Directory.CreateDirectory(logpath);

        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        using (_serialPort) { }
        _logger.LogInformation("PFC.WorkerService stop at: {time}", DateTimeOffset.Now);
        // ここにサービス終了時の処理を書く
        return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(_portOptions.Name)) throw new ArgumentNullException(nameof(PortOptions.Name));
        if (!SerialPort.GetPortNames().Contains(_portOptions.Name)) throw new ArgumentException($"Not Found Port:{_portOptions.Name}");

        _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

        _serialPort = new SerialPort(_portOptions.Name, 19200, Parity.Even, 8, StopBits.One);
        
        _serialPort.ReadTimeout = _portOptions.ReadTimeout;
        _serialPort.WriteTimeout = _portOptions.WriteTimeout;
        _serialPort.Open();
        _serialPort.DiscardOutBuffer();
        _serialPort.DiscardInBuffer();

        await Task.Delay(1000);

        var sw = new Stopwatch();
        while (!stoppingToken.IsCancellationRequested && _serialPort.IsOpen)
        {
            sw.Restart();

            var data = RequestAndReceiveData();
            if (data.IsValid)
            {
                _writer.WriteToMemoryAndFile(data);

                _queueDatas.EnqueueLifeTime(data);
                var (max, min) = _queueDatas.GetMaxMinValuesData();
            }

            sw.Stop();

            var fuelc = data.FuelCorrection;


            //var waitMs = _portOptions.PFCInterval - sw.ElapsedMilliseconds;
            //await Task.Delay((int)waitMs, stoppingToken);
            await Task.Delay(_portOptions.PFCInterval, stoppingToken);
        }
    }

    private AdvancedData RequestAndReceiveData()
    { 
        _serialPort.Write(AdvancedData.Command, 0, AdvancedData.Command.Length);
        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        
        byte[] buff = new byte[AdvancedData.RawDataLength];
        _serialPort.Read(buff, 0, AdvancedData.RawDataLength);

        return new AdvancedData(now, buff);
    }

    public override void Dispose()
    {
        Terminate();
        base.Dispose();
    }

    private void Terminate()
    {
        using (_writer)
        using (_serialPort)
        { }
    }

    // コントロールハンドラ
    private bool ControlHandler(CtrlTypes ctrlType)
    {
        switch (ctrlType)
        {
            case CtrlTypes.CTRL_C_EVENT:
                // Console.WriteLine("Ctrl+Cが押されました");
                // キャンセルする場合はtrue
                return false;
            case CtrlTypes.CTRL_BREAK_EVENT:
                //Console.WriteLine("Ctrl+Breakが押されました");
                // キャンセルする場合はtrue
                return false;
            case CtrlTypes.CTRL_CLOSE_EVENT:
                // .NETはuser32.dll/gdi32.dllがロードされるためLOGOFF/SHUTDOWNは利用不可
                //case ConsoleAPI.CtrlTypes.CTRL_LOGOFF_EVENT:
                //case ConsoleAPI.CtrlTypes.CTRL_SHUTDOWN_EVENT:

                Terminate();
                Thread.Sleep(300000);
                return true;
        }
        return false;
    }
}

public class PortOptions
{
    public const string Section = "PortOption";

    public string? Name { get; set; }
    public int ReadTimeout { get; set; }
    public int WriteTimeout { get; set; }
    public int PFCInterval { get; set; }
}

record AdvancedDataValues(int AirTemp, float BattVoltage, float Boost, float BoostDutyTP, float BoostDutyWG, float FuelCorrection, int FuelTemp, int IGNAngleLd, int IGNAngleTr,
    float InjectorWidthPrimary, float InjectorWidthSecondary, float ISCVDuty, int KnockLevel, int MapSensorVoltage, float MOPPosition, float O2Voltage, int Rpm, int Speed,
    int ThrottleSensorVoltage, int WaterTemp);

file static class QueueExtensions
{
    private const int _lifeTimeMs = 3000;

    public static void EnqueueLifeTime(this Queue<AdvancedData> queue, AdvancedData item)
    {
        var limit = item.ReceivedUnixTimeMs - _lifeTimeMs;
        queue.Enqueue(item);

        while (queue.Peek().ReceivedUnixTimeMs < limit)
        {
            queue.Dequeue();
        }
    }

    public static (AdvancedDataValues MaxValues, AdvancedDataValues MinValues) GetMaxMinValuesData(this Queue<AdvancedData> queue)
    {
        int maxAirTemp = 0, maxFuelTemp = 0, maxIGNAngleLd = 0, maxIGNAngleTr = 0, maxKnockLevel = 0, maxMapSensorVoltage = 0, maxRpm = 0, maxSpeed = 0,
            maxThrottleSensorVoltage = 0, maxWaterTemp = 0;
        float maxBattVoltage = 0, maxBoost = 0, maxBoostDutyTP = 0, maxBoostDutyWG = 0, maxFuelCorrection = 0, maxInjectorWidthPrimary = 0,
            maxInjectorWidthSecondary = 0, maxISCVDuty = 0, maxMOPPosition = 0, maxO2Voltage = 0;

        int minAirTemp = 0, minFuelTemp = 0, minIGNAngleLd = 0, minIGNAngleTr = 0, minKnockLevel = 0, minMapSensorVoltage = 0, minRpm = 0, minSpeed = 0,
            minThrottleSensorVoltage = 0, minWaterTemp = 0;
        float minBattVoltage = 0, minBoost = 0, minBoostDutyTP = 0, minBoostDutyWG = 0, minFuelCorrection = 0, minInjectorWidthPrimary = 0,
            minInjectorWidthSecondary = 0, minISCVDuty = 0, minMOPPosition = 0, minO2Voltage = 0;

        foreach (var item in queue)
        {
            if (maxAirTemp < item.AirTemp) maxAirTemp = item.AirTemp;
            if (maxBattVoltage < item.BattVoltage) maxBattVoltage = item.BattVoltage;
            if (maxBoost < item.Boost) maxBoost = item.Boost;
            if (maxBoostDutyTP < item.BoostDutyTP) maxBoostDutyTP = item.BoostDutyTP;
            if (maxBoostDutyWG < item.BoostDutyWG) maxBoostDutyWG = item.BoostDutyWG;
            if (maxFuelCorrection < item.FuelCorrection) maxFuelCorrection = item.FuelCorrection;
            if (maxFuelTemp < item.FuelTemp) maxFuelTemp = item.FuelTemp;
            if (maxIGNAngleLd < item.IGNAngleLd) maxIGNAngleLd = item.IGNAngleLd;
            if (maxIGNAngleTr < item.IGNAngleTr) maxIGNAngleTr = item.IGNAngleTr;
            if (maxInjectorWidthPrimary < item.InjectorWidthPrimary) maxInjectorWidthPrimary = item.InjectorWidthPrimary;
            if (maxInjectorWidthSecondary < item.InjectorWidthSecondary) maxInjectorWidthSecondary = item.InjectorWidthSecondary;
            if (maxISCVDuty < item.ISCVDuty) maxISCVDuty = item.ISCVDuty;
            if (maxKnockLevel < item.KnockLevel) maxKnockLevel = item.KnockLevel;
            if (maxMapSensorVoltage < item.MapSensorVoltage) maxMapSensorVoltage = item.MapSensorVoltage;
            if (maxMOPPosition < item.MOPPosition) maxMOPPosition = item.MOPPosition;
            if (maxO2Voltage < item.O2Voltage) maxO2Voltage = item.O2Voltage;
            if (maxRpm < item.Rpm) maxRpm = item.Rpm;
            if (maxSpeed < item.Speed) maxSpeed = item.Speed;
            if (maxThrottleSensorVoltage < item.ThrottleSensorVoltage) maxThrottleSensorVoltage = item.ThrottleSensorVoltage;
            if (maxWaterTemp < item.WaterTemp) maxWaterTemp = item.WaterTemp;

            if (minAirTemp > item.AirTemp) minAirTemp = item.AirTemp;
            if (minBattVoltage > item.BattVoltage) minBattVoltage = item.BattVoltage;
            if (minBoost > item.Boost) minBoost = item.Boost;
            if (minBoostDutyTP > item.BoostDutyTP) minBoostDutyTP = item.BoostDutyTP;
            if (minBoostDutyWG > item.BoostDutyWG) minBoostDutyWG = item.BoostDutyWG;
            if (minFuelCorrection > item.FuelCorrection) minFuelCorrection = item.FuelCorrection;
            if (minFuelTemp > item.FuelTemp) minFuelTemp = item.FuelTemp;
            if (minIGNAngleLd > item.IGNAngleLd) minIGNAngleLd = item.IGNAngleLd;
            if (minIGNAngleTr > item.IGNAngleTr) minIGNAngleTr = item.IGNAngleTr;
            if (minInjectorWidthPrimary > item.InjectorWidthPrimary) minInjectorWidthPrimary = item.InjectorWidthPrimary;
            if (minInjectorWidthSecondary > item.InjectorWidthSecondary) minInjectorWidthSecondary = item.InjectorWidthSecondary;
            if (minISCVDuty > item.ISCVDuty) minISCVDuty = item.ISCVDuty;
            if (minKnockLevel > item.KnockLevel) minKnockLevel = item.KnockLevel;
            if (minMapSensorVoltage > item.MapSensorVoltage) minMapSensorVoltage = item.MapSensorVoltage;
            if (minMOPPosition > item.MOPPosition) minMOPPosition = item.MOPPosition;
            if (minO2Voltage > item.O2Voltage) minO2Voltage = item.O2Voltage;
            if (minRpm > item.Rpm) minRpm = item.Rpm;
            if (minSpeed > item.Speed) minSpeed = item.Speed;
            if (minThrottleSensorVoltage > item.ThrottleSensorVoltage) minThrottleSensorVoltage = item.ThrottleSensorVoltage;
            if (minWaterTemp > item.WaterTemp) minWaterTemp = item.WaterTemp;
        }

        var max = new AdvancedDataValues(maxAirTemp, maxBattVoltage, maxBoost, maxBoostDutyTP, maxBoostDutyWG, maxFuelCorrection, maxFuelTemp, maxIGNAngleLd, maxIGNAngleTr, 
            maxInjectorWidthPrimary, maxInjectorWidthSecondary, maxISCVDuty, maxKnockLevel, maxMapSensorVoltage, maxMOPPosition, maxO2Voltage, maxRpm, maxSpeed,
            maxThrottleSensorVoltage, maxWaterTemp);
        var min = new AdvancedDataValues(minAirTemp, minBattVoltage, minBoost, minBoostDutyTP, minBoostDutyWG, minFuelCorrection, minFuelTemp, minIGNAngleLd, minIGNAngleTr,
            minInjectorWidthPrimary, minInjectorWidthSecondary, minISCVDuty, minKnockLevel, minMapSensorVoltage, minMOPPosition, minO2Voltage, minRpm, minSpeed,
            minThrottleSensorVoltage, minWaterTemp);
        return (max, min);
    }
}