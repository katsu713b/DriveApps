using Microsoft.Extensions.Options;
using PFC;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using Microsoft.Extensions.Hosting;
using System.Runtime.CompilerServices;
using System.Windows.Documents;

namespace DriveApp.Dash.PFC;

/// <summary>
/// PCFから読み取った情報をファイル出力する
/// 
/// </summary>
public class PFCProvider : BackgroundService
{
    readonly PFCOption _pFCOptions;
    SerialPort _serialPort;
    readonly PFCLogWriter _writer;
    Queue<AdvancedData> _queueDatas = new Queue<AdvancedData>();
    readonly PFCContext _pFCContext;
    Stopwatch _sw = new Stopwatch();
    static object _lock = new object();
    static readonly AsyncLock asynclock = new AsyncLock();
    ConcurrentQueue<int> _queueInterruptWait = new ConcurrentQueue<int>();
    TimeSpan _startTime = TimeSpan.Zero;
    TimeSpan _endTime = TimeSpan.Zero;


    public PFCProvider(IOptionsMonitor<PFCOption> options, PFCContext context, PFCLogWriter writer)
    {
        _pFCOptions = options.CurrentValue;
        _writer = writer;
        _pFCContext = context;
        _pFCContext.OnInterruptWrite += PFCContext_OnInterruptWrite;
        _pFCContext.OnApplicationShutDown += _pFCContext_OnApplicationShutDown;
        _serialPort = new SerialPort();
    }

    private void _pFCContext_OnApplicationShutDown()
    {
        if (_serialPort.IsOpen)
            _serialPort.Close();

        using (_serialPort) { }
        using (_writer) { }
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        /*
         PFC未接続 <-> 接続
         CMD未接続 <-> 接続
         
        while
        {
          PFC未接続 continue;
          
          CMD情報取得

          while
          {
            割り込み break;
            PFC切断 break;

            polling / 100ms
          }
        }
        
         */

        var logpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!Directory.Exists(logpath)) Directory.CreateDirectory(logpath);

        _sw.Start();
        _endTime = _sw.Elapsed;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConnectPFC();

                // Commander情報取得
                // TODO: json file
                await GetCommanderInfo(ct);

                await Task.Delay(500);

                await Initial(ct);

                _pFCContext.IsPFCConnected = true;
                _pFCContext.StartPolling = true;

                await PollingAdvanceDataAsync(ct);
            }
            catch
            {
                // port error
                _pFCContext.IsPFCConnected = false;
                await Task.Delay(1000, ct);
            }
            finally
            {
                _pFCContext.StartPolling = false;
            }
        }

        async Task ConnectPFC()
        {
            while (!ct.IsCancellationRequested)
            {
                // TODO: message
                if (string.IsNullOrEmpty(_pFCOptions.PFCPort.Name))
                {
                    await Task.Delay(1000, ct);
                    continue;
                }

                // TODO: message
                if (!SerialPort.GetPortNames().Contains(_pFCOptions.PFCPort.Name))
                {
                    await Task.Delay(1000, ct);
                    continue;
                }
                break;
            }

            if (!_serialPort.IsOpen)
            {
                _serialPort.SetUpPFC(_pFCOptions.PFCPort.Name, _pFCOptions.PFCPort.ReadTimeout, _pFCOptions.PFCPort.WriteTimeout);
                _serialPort.Open();
            }
        }
    }

    private static readonly byte[][] COMMANDER_CMD = new byte[][]
    {
        new byte[] { 0xD7, 0x2, 0x26 },
        new byte[] { 0xD8, 0x2, 0x25 },
        new byte[] { 0xD9, 0x2, 0x24 },
        new byte[] { 0xCA, 0x2, 0x33 },
        new byte[] { 0xF3, 0x2, 0x0A },
        new byte[] { 0xF5, 0x2, 0x08 }
    };

    private async Task GetCommanderInfo(CancellationToken ct)
    {
        using (await asynclock.LockAsync())
        {
            foreach (var cmd in COMMANDER_CMD)
            {
                for (var i = 0; i < 3; i++)
                {
                    if (ct.IsCancellationRequested) return;

                    var res = WriteAndReadData(cmd);
                    if (res.Length == 0)
                    {
                        await Task.Delay(300, ct);
                        continue;
                    }
                    _pFCContext.CommanderInfo[BitConverter.ToString(cmd)] = res;
                    break;
                }
                // TODO: 不要なはず
                await Task.Delay(50);
            }
        }
    }

    private static readonly byte[][] INITIAL_CMD = new byte[][]
    {
        new byte[] { 0xF3, 0x2, 0x0A },
        new byte[] { 0xF4, 0x2, 0x9 },
        new byte[] { 0xF5, 0x2, 0x8 }
    };

    private async Task Initial(CancellationToken ct)
    {
        using (await asynclock.LockAsync())
        {
            foreach (var cmd in INITIAL_CMD)
            {
                for (var i = 0; i < 3; i++)
                {
                    if (ct.IsCancellationRequested) return;

                    var res = WriteAndReadData(cmd);
                    if (res.Length == 0)
                    {
                        await Task.Delay(300, ct);
                        continue;
                    }
                    break;
                }
                // TODO: 不要なはず
                await Task.Delay(50);
            }
        }
    }

    private static readonly byte[] EmptyData = Enumerable.Empty<byte>().ToArray();

    private byte[] WriteAndReadData(byte[] cmd, int retryCount = 2)
    {
        _serialPort.DiscardOutBuffer();
        _serialPort.DiscardInBuffer();

        _writer.WriteOperationLog(cmd, OperationType.ToPFC);
        var successWrite = WriteCmd();
        if (!successWrite)
            return EmptyData;
        var res = _serialPort.Read();
        if (res.Length > 0)
            _writer.WriteOperationLog(cmd, OperationType.FromPFC);

        return res;

        bool WriteCmd()
        {
            for (int i = 0; i < retryCount + 1; i++)
            {
                try
                {
                    _serialPort.Write(cmd, 0, cmd.Length);
                    return true;
                }
                catch
                {
                }
            }
            return false;
        }
    }

    private async Task PollingAdvanceDataAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (!_pFCContext.StartPolling)
            {
                await Task.Delay(100);
                continue;
            }

            if (_pFCContext.LatestAdvancedData == null || _pFCContext.LatestAdvancedData.Rpm < 500)
            {
                await Task.Delay(1000);
            }

            using (await asynclock.LockAsync())
            {
                if (!_pFCContext.StartPolling)
                {
                    continue;
                }

                var waitMs = Math.Max(_pFCOptions.PFCPort.PFCInterval - (int)((_endTime - _startTime).TotalMilliseconds + 0.5), 15);
                await Task.Delay(waitMs, ct);

                if (ct.IsCancellationRequested) return;

                _startTime = _sw.Elapsed;

                var res = WriteAndReadData(AdvancedData.Command);

                if (res.Length == 0)
                {
                    _endTime = _sw.Elapsed;
                    continue;
                }

                var adata = new AdvancedData(DateTimeOffset.Now.ToUnixTimeMilliseconds(), res);

                if (adata.IsValid)
                {
                    _pFCContext.LatestAdvancedData = adata;
                    _writer.WriteLogger(adata);

                    //_queueDatas.EnqueueLifeTime(data);
                    //var (max, min) = _queueDatas.GetMaxMinValuesData();
                }

                _pFCContext.IsPFCConnected = true;
                _endTime = _sw.Elapsed;
            }
        }
    }

    private int ElapsedFromLast => (int)(_sw.Elapsed - _endTime).TotalMilliseconds;

    private async Task<byte[]> PFCContext_OnInterruptWrite(byte[] cmd)
    {
        // センサーデータ取得時はポーリング中断
        //_pFCContext.StartPolling = !(cmd[0] == SensorData.Command[0]);
        using (await asynclock.LockAsync())
        {
            // センサーデータ取得時はポーリング中断
            //_pFCContext.StartPolling = !(cmd[0] == SensorData.Command[0]);

            // コマンダー起動
            var cmdTxt = BitConverter.ToString(cmd);
            if (_pFCContext.CommanderInfo.TryGetValue(cmdTxt, out var cmdInfo))
            {
                _pFCContext.CommanderInfo.Remove(cmdTxt);
                return cmdInfo;
            }

            //var delay = 50 - Math.Max(0, ElapsedFromLast);
            //if (delay > 0)
            //    await Task.Delay(delay);

            _startTime = _sw.Elapsed;

            var res = WriteAndReadData(cmd, 0);
            
            _endTime = _sw.Elapsed;

            return res;
        }
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
    private const int _lifeTimeMs = 3 * 60 * 1000;

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

/// <summary>
/// async な文脈での lock を提供します．
/// Lock 開放のために，必ず処理の完了後に LockAsync が生成した IDisposable を Dispose してください．
/// </summary>
public sealed class AsyncLock
{
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public async Task<IDisposable> LockAsync()
    {
        await _semaphore.WaitAsync();
        return new Handler(_semaphore);
    }

    private sealed class Handler : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed = false;

        public Handler(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _semaphore.Release();
                _disposed = true;
            }
        }
    }
}