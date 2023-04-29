using DriveApp.GPSLapTimer.Core.Circuits;
using DriveApp.GPSLapTimer.Core.Gps;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DriveApp.GPSLapTimer.Logger
{

    internal class GpsLogWriter
    {
        // loggingの仕様
        // 停止が１分継続 => stop & filerotation
        //   1 * 60s * 10hz = 600messages
        // 走行中 => logging
        //   gpslog_yyyyMMddss_{circuitName}.log

        private ConcurrentQueue<string> _queue;
        private const int Capacity = 600;
        private const string TemplateLogFileName = "gpslog_%DATE_TIME%_%CIRCUIT_NAME%.nmea";
        private bool _reserveRotation = true;
        private bool _start = false;
        private int _stoppedCount = 0;
        private Circuit _currentCircuit;

        public GpsLogWriter()
        {
            _queue = new ConcurrentQueue<string>();
        }

        public void EnterCircuit(Circuit circuit)
        {
            _queue.Clear();
            _currentCircuit = circuit;
            _reserveRotation = true;
        }

        public void LeaveCircuit()
        {
            _start = false;
            _currentCircuit = null;
            _reserveRotation = true;
        }

        public Task Execute(CancellationToken ct)
        {
            return Task.Run(() => ExecuteInner(ct));
        }

        private void ExecuteInner(CancellationToken ct)
        {
            var logPath = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "logs");

            while (!ct.IsCancellationRequested)
            {
                WaitStart();

                if (!Directory.Exists(logPath))
                    Directory.CreateDirectory(logPath);

                var fileName = TemplateLogFileName
                    .Replace("%DATE_TIME%", DateTime.Now.ToString("yyyyMMddHHmmss"))
                    .Replace("%CIRCUIT_NAME%", _currentCircuit?.Name);

                _reserveRotation = false;

                Write(Path.Combine(logPath, fileName));
            }

            void WaitStart()
            {
                while (!_start && !ct.IsCancellationRequested)
                {
                    Thread.Sleep(5000);
                }
            }

            void Write(string fileName)
            {
                while (!_reserveRotation && !ct.IsCancellationRequested)
                {
                    while (_queue.TryDequeue(out var log))
                    {
                        File.AppendAllText(fileName, log);
                    }
                    Thread.Sleep(1000);
                }
            }
        }

        public void OnGpsChanged(GpsValue gps)
        {
            _queue.Enqueue(gps.RawText + "\n");

            if (_start)
            {
                if (IsDriving())
                {
                    _stoppedCount = 0;
                    return;
                }

                _stoppedCount++;

                if (_stoppedCount >= 600)
                {
                    _start = false;
                    _reserveRotation = true;
                }
            }
            else
            {
                if (_queue.Count >= Capacity)
                {
                    _queue.TryDequeue(out var _);
                }
                _start = IsDriving();
            }

            bool IsDriving() => gps.Speed >= 0.7;
        }
    }

}
