using DriveApp.GPSLapTimer.Core.Circuits;
using DriveApp.GPSLapTimer.Core.Gps;
using DriveApp.GPSLapTimer.Infrastructure;
using DriveApp.GPSLapTimer.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

[assembly: InternalsVisibleTo("GPSLapTimerTest")]

namespace DriveApp.GPSLapTimer.BackgroundService
{
    /// <summary>
    /// 
    /// TODO: LapTime表をログ出力
    /// </summary>
    internal class GpsLapCounter : IDisposable
    {
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private IGpsReceiver _gpsReceiver;
        private GpsValue _gps = GpsValueFactory.CreateValue("", null);
        private int _lapCount = 0;
        private TimeSpan _fastest = TimeSpan.MaxValue;
        private float _maxSpeed = 0;
        private Stopwatch _sw = new Stopwatch();
        private List<LapTime> _lapTimes = new List<LapTime>();
        private Circuit _currentCircuit;
        private LocationWorker _locationWorker;
        private GpsLogWriter _logging;
        private GpsAnalysis _analysis;

        public GpsLapCounter(IGpsReceiver receiver)
        {
            _gpsReceiver = receiver;

            _locationWorker = new LocationWorker();
            _locationWorker.OnLocationEnter += _locationChecker_OnLocationEnter;
            _locationWorker.OnLocationLeave += _locationChecker_OnLocationLeave;

            _logging = new GpsLogWriter();
            _locationWorker.OnLocationEnter += _logging.EnterCircuit;
            _locationWorker.OnLocationLeave += _logging.LeaveCircuit;

            _analysis = new GpsAnalysis();

            OnGpsValueChanged += _logging.OnGpsChanged;
            OnGpsValueChanged += _locationWorker.OnGpsChanged;
            OnGpsValueChanged += _analysis.OnGpsChanged;
        }

        private static readonly object _lock = new object();

        private void _locationChecker_OnLocationEnter(Circuit circuit)
        {
            // start MeasurementTimeWithSector
            lock (_lock)
            {
                _currentCircuit = circuit;
            }
        }

        private void _locationChecker_OnLocationLeave()
        {
            // stop MeasurementTimeWithSector
            lock (_lock)
            {
                _currentCircuit = null;
            }
        }

        public void Run()
        {
            try
            {
                _gpsReceiver.Open();
                _locationWorker.Start(_cts.Token).ConfigureAwait(false);
                _logging.Execute(_cts.Token).ConfigureAwait(false);

                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        WaitForEnterCircuit();
                        if (_cts.IsCancellationRequested) break;

                        MeasurementTimeWithSector();
                    }
                    catch (TimeoutException) { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            CloseSerial();
        }

        private void ReceiveAndUpdate()
        {
            do
            {
                _gps = GpsValueFactory.CreateValue(_gpsReceiver.ReadLine(), _gps.Geo);
            }
            while (!_gps.IsValid);

            if (_gps.Speed > _maxSpeed)
            {
                _maxSpeed = _gps.Speed;
            }

            OnGpsValueChanged(_gps);
        }

        /// <summary>
        /// 現在地のサーキットを取得する
        /// </summary>
        private void WaitForEnterCircuit()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    ReceiveAndUpdate();
                    if (_cts.IsCancellationRequested) return;
                    if (_currentCircuit != null) break;
                }
                catch (TimeoutException) { }
            }

            return;
        }


        private void MeasurementTimeWithSector()
        {
            _lapCount = 0;
            if (_currentCircuit == null) return;

            try
            {
                while (_currentCircuit != null && !_currentCircuit.PassControlLine(_gps.Seg))
                {
                    ReceiveAndUpdate();
                    if (_cts.IsCancellationRequested) return;
                }

                if (_currentCircuit == null) return;

                _sw.Start();

                while (!_cts.IsCancellationRequested)
                {
                    bool passedS1 = false, passedS2 = false;
                    float tmpMaxSpeed = _gps.Speed, tmpMinSpeed = _gps.Speed;

                    var time = new LapTime(++_lapCount, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
                    _lapTimes.Add(time);

                    ReceiveAndUpdate();

                    while (!_currentCircuit.PassControlLine(_gps.Seg))
                    {
                        tmpMaxSpeed = Math.Max(tmpMaxSpeed, _gps.Speed);
                        tmpMinSpeed = Math.Min(tmpMinSpeed, _gps.Speed);

                        // sector計測
                        if (!passedS1 && _currentCircuit.PassSec1Line(_gps.Seg))
                        {
                            // ミリ秒以下切り捨て
                            time.S1 = new TimeSpan((_sw.ElapsedTicks / 10000) * 10000);
                            passedS1 = true;
                            time.MaxSpeedS1 = tmpMaxSpeed;
                            time.MinSpeedS1 = tmpMinSpeed;
                            tmpMaxSpeed = 0;
                            tmpMinSpeed = float.MaxValue;
                        }
                        else if (passedS1 && !passedS2 && _currentCircuit.PassSec2Line(_gps.Seg))
                        {
                            //time.S2 = _sw.Elapsed - time.S1;
                            time.S2 = new TimeSpan(((_sw.ElapsedTicks - time.S1.Ticks) / 10000) * 10000);
                            passedS2 = true;
                            time.MaxSpeedS2 = tmpMaxSpeed;
                            time.MinSpeedS2 = tmpMinSpeed;
                            tmpMaxSpeed = 0;
                            tmpMinSpeed = float.MaxValue;
                        }

                        ReceiveAndUpdate();
                        if (_cts.IsCancellationRequested) return;
                    }

                    time.Time = new TimeSpan((_sw.ElapsedTicks / 10000) * 10000);
                    _sw.Restart();

                    if (passedS1 && passedS2)
                    {
                        time.S3 = time.Time - time.S1 - time.S2;
                        time.MaxSpeedS3 = tmpMaxSpeed;
                        time.MinSpeedS3 = tmpMinSpeed;
                    }

                    if (_fastest > time.Time)
                    {
                        _fastest = time.Time;
                    }
                    time.Write(_currentCircuit.Name);
                }
            }
            catch (TimeoutException) { }
        }

        //private TimeSpan FixStartTime(Vector2 line, Vector2 current, Vector2 before, TimeSpan currentTime, TimeSpan beforeTime)
        //{
        //    var cr = Vector2.Dot(line, current);
        //    var crS2 = (float)(current.X * current.X + current.Y * current.Y);
        //    var ct = Math.Sqrt(crS2 - cr);

        //    var br = Vector2.Dot(line, before);
        //    var bS2 = (float)(before.X * before.X + before.Y * before.Y);
        //    var bt = Math.Sqrt(bS2 - br);

        //    var t = currentTime - beforeTime;
        //    t = t * ct / (ct + bt);
        //    return currentTime - t;
        //}

        public void Dispose()
        {
            _cts.Cancel();
            CloseSerial();
        }

        private void CloseSerial()
        {
            if (_gpsReceiver.IsOpen)
            {
                _gpsReceiver.Close();
            }
        }

        /// <summary>
        /// 現在走行中のLap数
        /// つまり LapTimes には (LapCount - 1)数だけタイムが入っている
        /// </summary>
        public int LapCount => _lapCount;
        public List<LapTime> LapTimes => _lapTimes;
        public string RawGpsText => _gps.RawText;
        public float CurrentSpeed => _gps.Speed;
        public float MaxSpeed => _maxSpeed;
        public TimeSpan FastestTime => _fastest;
        public TimeSpan CurrentTime => _sw.Elapsed;
        public string CircuitName => _currentCircuit?.Name ?? string.Empty;
        public float G => _analysis.G;
        public float Gy => _analysis.Gy;
        public float Gx => _analysis.Gx;

        public delegate void GpsValueChangedHandler(GpsValue gps);
        public event GpsValueChangedHandler OnGpsValueChanged;
    }

    internal class LapTime
    {
        public LapTime(int count, TimeSpan time, TimeSpan s1, TimeSpan s2, TimeSpan s3)
        {
            LapCount = count;
            Time = time;
            S1 = s1;
            S2 = s2;
            S3 = s3;
        }
        public int LapCount { get; set; }
        public TimeSpan Time { get; set; }
        public TimeSpan S1 { get; set; }
        public TimeSpan S2 { get; set; }
        public TimeSpan S3 { get; set; }
        public float MaxSpeedS1 { get; set; }
        public float MinSpeedS1 { get; set; }
        public float MaxSpeedS2 { get; set; }
        public float MinSpeedS2 { get; set; }
        public float MaxSpeedS3 { get; set; }
        public float MinSpeedS3 { get; set; }
        public float MaxSpeed => Math.Max(MaxSpeedS1, Math.Max(MaxSpeedS2, MaxSpeedS3));
        public float MinSpeed => Math.Min(MinSpeedS1, Math.Min(MinSpeedS2, MinSpeedS3));

        private const string TemplateLapTimeFileName = "laptime_%DATE%_%CIRCUIT_NAME%.txt";
        private const string _fomatSec = @"s\.fff";
        private const string _fomatMin = @"m\:ss\.fff";

        public void Write(string circuitName)
        {
            try
            {
                var logPath = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "logs");

                if (!Directory.Exists(logPath))
                    Directory.CreateDirectory(logPath);

                var fileName = Path.Combine(logPath, TemplateLapTimeFileName
                    .Replace("%DATE%", DateTime.Now.ToString("yyyyMMdd"))
                    .Replace("%CIRCUIT_NAME%", circuitName));

                if (!File.Exists(fileName))
                    File.AppendAllText(fileName, "DateTime,LapCount,Time,S1,MaxS1,MinS1,S2,MaxS2,MinS2,S3,MaxS3,MinS3\n");

                var time = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")},{LapCount},{ToStringLapTime(Time)},{ToStringLapTime(S1)},{MaxSpeedS1},{MinSpeedS1},{ToStringLapTime(S2)},{MaxSpeedS2},{MinSpeedS2},{ToStringLapTime(S3)},{MaxSpeedS3},{MinSpeedS3}\n";
                File.AppendAllText(fileName, time);
            }
            catch { }

            string ToStringLapTime(TimeSpan ts)
            {
                if (ts == TimeSpan.Zero) return string.Empty;
                return ts.ToString(ts.TotalMinutes > 1 ? _fomatMin : _fomatSec);
            }
        }
    }
}
