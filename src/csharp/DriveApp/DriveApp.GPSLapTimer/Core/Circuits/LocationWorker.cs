using DriveApp.GPSLapTimer.Core.Gps;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DriveApp.GPSLapTimer.Core.Circuits
{
    internal class LocationWorker
    {
        public delegate void LocationEnterHandler(Circuit circuit);
        public delegate void LocationLeaveHandler();

        public event LocationEnterHandler OnLocationEnter;
        public event LocationLeaveHandler OnLocationLeave;

        public LocationWorker() { }

        private GpsValue _gps;

        public void OnGpsChanged(GpsValue gps)
        {
            _gps = gps;
        }

        public Task Start(CancellationToken ct)
        {
            return Task.Run(() => EventLoop(ct));
        }

        void EventLoop(CancellationToken ct)
        {
            var allLines = CircuitLocation.AllCircuit;

            while (!ct.IsCancellationRequested && _gps == null) Thread.Sleep(500);

            while (!ct.IsCancellationRequested)
            {
                var circuit = WaitForEnter();
                if (ct.IsCancellationRequested) return;
                if (circuit == null) return;

                OnLocationEnter(circuit);

                WaitForLeave(circuit);

                OnLocationLeave();
            }

            Circuit WaitForEnter()
            {
                while (!ct.IsCancellationRequested)
                {
                    var circuit = allLines.FirstOrDefault((c) => OnTheArea(_gps.Geo, c.GetAreaPoints()));
                    if (circuit != null) return circuit;

                    Thread.Sleep(5000);
                }

                return null;
            }

            void WaitForLeave(Circuit c)
            {
                while (!ct.IsCancellationRequested)
                {
                    if (!OnTheArea(_gps.Geo, c.GetAreaPoints()))
                    {
                        break;
                    }

                    Thread.Sleep(60000);
                }
            }
        }

        internal static bool OnTheArea(GeoPoint target, IEnumerable<(GeoPoint P1, GeoPoint P2)> area)
        {
            // 現在地を (x, y) = (0, 0)とした場合、xがプラス方向にあるy=0と交差する線分があるか
            // 奇数点ある場合、現在地がエリア内にいる
            var ret = false;

            foreach (var (point1, point2) in area)
            {
                (double X, double Y) p1 = ((point1.Longitude - target.Longitude) * 1000, (point1.Latitude - target.Latitude) * 1000);
                (double X, double Y) p2 = ((point2.Longitude - target.Longitude) * 1000, (point2.Latitude - target.Latitude) * 1000);

                if (p1.X <= 0 && p2.X <= 0)
                {
                    continue;
                }
                if (p1.Y >= 0 && p2.Y >= 0)
                {
                    continue;
                }
                if (p1.Y <= 0 && p2.Y <= 0)
                {
                    continue;
                }

                if (p1.X > 0 && p2.X > 0)
                {
                    ret ^= true;
                    continue;
                }

                var a = (p2.Y - p1.Y) / (p2.X - p1.X);
                var b = p1.Y - a * p1.X;
                var y0 = -b / a;

                ret ^= y0 > 0;
            }

            return ret;
        }
    }
}
