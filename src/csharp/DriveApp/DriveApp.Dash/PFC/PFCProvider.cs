using Microsoft.Extensions.Options;
using PFC;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;

namespace DriveApp.Dash.PFC;

/// <summary>
/// PCFから読み取った情報をファイル出力する
/// 
/// </summary>
public class PFCProvider : BackgroundService
{
    private readonly PFCOption _pFCOptions;
    private SerialPort _serialPort;
    private PFCLogWriter _writer;
    private Queue<AdvancedData> _queueDatas = new Queue<AdvancedData>();
    private readonly PFCContext _pFCContext;
    private Stopwatch _sw = new Stopwatch();
    private static object _lock = new object();
    private ConcurrentQueue<int> _queueInterruptWait = new ConcurrentQueue<int>();

    public PFCProvider(IOptionsMonitor<PFCOption> options, PFCLogWriter writer, PFCContext context)
    {
        _pFCOptions = options.CurrentValue;
        _writer = writer;
        _pFCContext = context;
        _pFCContext.OnInterruptWrite += PFCContext_OnInterruptWrite;
    }
    
    TimeSpan _startTime = TimeSpan.MinValue;
    TimeSpan _endTime = TimeSpan.MinValue;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var logpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!Directory.Exists(logpath)) Directory.CreateDirectory(logpath);

        if (string.IsNullOrEmpty(_pFCOptions.PFCPort.Name)) throw new ArgumentNullException(nameof(PFCOption.PFCPort.Name));
        if (!SerialPort.GetPortNames().Contains(_pFCOptions.PFCPort.Name)) throw new ArgumentException($"Not Found Port:{_pFCOptions.PFCPort.Name}");

        _serialPort = new SerialPort(_pFCOptions.PFCPort.Name, 19200, Parity.Even, 8, StopBits.One);
        
        _serialPort.ReadTimeout = _pFCOptions.PFCPort.ReadTimeout;
        _serialPort.WriteTimeout = _pFCOptions.PFCPort.WriteTimeout;
        _sw.Start();
        _endTime = _sw.Elapsed;

        await Task.Delay(800);

        _serialPort.Open();
        _serialPort.DiscardOutBuffer();
        _serialPort.DiscardInBuffer();

        int waitMinValue = (int)(_pFCOptions.PFCPort.PFCInterval * 0.9);

        while (!ct.IsCancellationRequested && _serialPort.IsOpen)
        {
            int waitMs = 0;

            while (_queueInterruptWait.TryDequeue(out var qwait))
            {
                await Task.Delay(qwait);
            }

            lock (_lock)
            {
                var elapsedLast = ElapsedFromLast;

                if (elapsedLast < waitMinValue)
                {
                    Thread.Sleep(_pFCOptions.PFCPort.PFCInterval - elapsedLast);
                }

                _startTime = _sw.Elapsed;

                var data = RequestAndReceiveData();
                if (data.IsValid)
                {
                    _pFCContext.LatestAdvancedData = data;
                    _writer.WriteToFile(data);

                    _queueDatas.EnqueueLifeTime(data);
                    var (max, min) = _queueDatas.GetMaxMinValuesData();
                }

                _endTime = _sw.Elapsed;

                //var fuelc = data.FuelCorrection;

                waitMs = Math.Max(_pFCOptions.PFCPort.PFCInterval - (int)((_endTime - _startTime).TotalMilliseconds + 0.5), 15);
                //await Task.Delay(_pFCOptions.PFCPort.PFCInterval, stoppingToken);
            }
            await Task.Delay(waitMs, ct);
        }
    }
    private int ElapsedFromLast => (int)(_sw.Elapsed - _endTime).TotalMilliseconds;

    private byte[] PFCContext_OnInterruptWrite(byte[] data)
    {
        const int iLen = 1;
        lock (_lock)
        {
            var isSensorPolling = data.SequenceEqual(SensorData.Command);
            if (isSensorPolling)
            {
                _queueInterruptWait.Enqueue(_pFCOptions.PFCPort.PFCInterval);
            }

            if (ElapsedFromLast < 50)
            {
                Thread.Sleep(50);
            }

            if (isSensorPolling)
                _startTime = _sw.Elapsed;

            byte[] buffer = new byte[256];

            _serialPort.DiscardInBuffer();
            _serialPort.Write(data, 0, data.Length);

            _serialPort.Read(buffer, 0, 2);
            _serialPort.Read(buffer, 2, buffer[iLen] - 1);

            Span<byte> span = buffer;
            var dataLen = span[iLen] + 1;
            
            if (isSensorPolling)
                _endTime = _sw.Elapsed;

            return span[..dataLen].ToArray();
        }
    }

    private AdvancedData RequestAndReceiveData()
    {
        _serialPort.DiscardInBuffer();
        _serialPort.Write(AdvancedData.Command, 0, AdvancedData.Command.Length);
        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();


        byte[] buff = new byte[AdvancedData.RawDataLength];
        _serialPort.Read(buff, 0, AdvancedData.RawDataLength);

        return new AdvancedData(now, buff);
    }

    public void Dispose()
    {
        Terminate();
    }

    private void Terminate()
    {
        using (_writer)
        using (_serialPort)
        { }
    }

    // コントロールハンドラ
    //private bool ControlHandler(CtrlTypes ctrlType)
    //{
    //    switch (ctrlType)
    //    {
    //        case CtrlTypes.CTRL_C_EVENT:
    //            // Console.WriteLine("Ctrl+Cが押されました");
    //            // キャンセルする場合はtrue
    //            return false;
    //        case CtrlTypes.CTRL_BREAK_EVENT:
    //            //Console.WriteLine("Ctrl+Breakが押されました");
    //            // キャンセルする場合はtrue
    //            return false;
    //        case CtrlTypes.CTRL_CLOSE_EVENT:
    //            // .NETはuser32.dll/gdi32.dllがロードされるためLOGOFF/SHUTDOWNは利用不可
    //            //case ConsoleAPI.CtrlTypes.CTRL_LOGOFF_EVENT:
    //            //case ConsoleAPI.CtrlTypes.CTRL_SHUTDOWN_EVENT:

    //            Terminate();
    //            Thread.Sleep(300000);
    //            return true;
    //    }
    //    return false;
    //}
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
        float maxBattVoltage = 0, maxAirIPress = 0, maxBoostDutyTP = 0, maxBoostDutyWG = 0, maxFuelCorrection = 0, maxInjectorWidthPrimary = 0,
            maxInjectorWidthSecondary = 0, maxISCVDuty = 0, maxMOPPosition = 0, maxO2Voltage = 0;

        int minAirTemp = 0, minFuelTemp = 0, minIGNAngleLd = 0, minIGNAngleTr = 0, minKnockLevel = 0, minMapSensorVoltage = 0, minRpm = 0, minSpeed = 0,
            minThrottleSensorVoltage = 0, minWaterTemp = 0;
        float minBattVoltage = 0, minAirIPress = 0, minBoostDutyTP = 0, minBoostDutyWG = 0, minFuelCorrection = 0, minInjectorWidthPrimary = 0,
            minInjectorWidthSecondary = 0, minISCVDuty = 0, minMOPPosition = 0, minO2Voltage = 0;

        foreach (var item in queue)
        {
            if (maxAirTemp < item.AirTemp) maxAirTemp = item.AirTemp;
            if (maxBattVoltage < item.BattVoltage) maxBattVoltage = item.BattVoltage;
            if (maxAirIPress < item.AirIPressure) maxAirIPress = item.AirIPressure;
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
            if (minAirIPress > item.AirIPressure) minAirIPress = item.AirIPressure;
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

        var max = new AdvancedDataValues(maxAirTemp, maxBattVoltage, maxAirIPress, maxBoostDutyTP, maxBoostDutyWG, maxFuelCorrection, maxFuelTemp, maxIGNAngleLd, maxIGNAngleTr, 
            maxInjectorWidthPrimary, maxInjectorWidthSecondary, maxISCVDuty, maxKnockLevel, maxMapSensorVoltage, maxMOPPosition, maxO2Voltage, maxRpm, maxSpeed,
            maxThrottleSensorVoltage, maxWaterTemp);
        var min = new AdvancedDataValues(minAirTemp, minBattVoltage, minAirIPress, minBoostDutyTP, minBoostDutyWG, minFuelCorrection, minFuelTemp, minIGNAngleLd, minIGNAngleTr,
            minInjectorWidthPrimary, minInjectorWidthSecondary, minISCVDuty, minKnockLevel, minMapSensorVoltage, minMOPPosition, minO2Voltage, minRpm, minSpeed,
            minThrottleSensorVoltage, minWaterTemp);
        return (max, min);
    }
}