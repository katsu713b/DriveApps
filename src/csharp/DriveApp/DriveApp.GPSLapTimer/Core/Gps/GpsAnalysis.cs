using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace DriveApp.GPSLapTimer.Core.Gps
{
    internal class GpsAnalysis
    {
        const int LIMIT_RADIUS_M = 3000;
        private ConcurrentQueue<GpsValue> _queueGps = new ConcurrentQueue<GpsValue>();
        private ConcurrentQueue<double> _queueR = new ConcurrentQueue<double>();
        private ConcurrentQueue<(float Y, float X)> _queueRawG = new ConcurrentQueue<(float Y, float X)>();
        private ConcurrentQueue<(float Y, float X)> _queueSmoothG = new ConcurrentQueue<(float Y, float X)>();

        public GpsAnalysis() { }

        public void OnGpsChanged(GpsValue gps)
        {
            //return;
            _queueGps.Enqueue(gps);
            Task.Run(Execute);
        }

        const int AVG_NUM = 3;
        const int SMOOTH_NUM = 2;

        private void Execute()
        {
            if (_queueGps.Count < Math.Max(AVG_NUM, SMOOTH_NUM))
            {
                _queueRawG.Enqueue((0, 0));
                _queueSmoothG.Enqueue((0, 0));
                _queueR.Enqueue(LIMIT_RADIUS_M);
                return;
            }

            while (_queueGps.Count > AVG_NUM)
            {
                _queueGps.TryDequeue(out var _);
            }
            
            var gpss = _queueGps.ToArray();

            var rawGy = gpss[2].Gy(gpss[1], 200); //TODO 100

            var r = gpss[2].Geo.RadiusMeters(gpss[1].Geo, gpss[0].Geo);
            _queueR.Enqueue(r);
            while (_queueR.Count > AVG_NUM)
            {
                _queueR.TryDequeue(out var _);
            }
            var smoothR = _queueR.ToArray().Average();
            //var smoothR = _queueR.ToArray().Max();
            smoothR = Math.Max(smoothR, r);
            var rawGx = gpss[2].Gx(gpss[1].Geo, gpss[0].Geo, smoothR);
            _queueRawG.Enqueue((rawGy, rawGx));

            while (_queueRawG.Count > AVG_NUM)
            {
                _queueRawG.TryDequeue(out var _);
            }

            var rawGs = _queueRawG.ToArray();
            var avgGy = rawGs.Average(g => g.Y);
            var avgGx = rawGs.Average(g => g.X);
            _queueSmoothG.Enqueue((avgGy, avgGx));

            while (_queueSmoothG.Count > SMOOTH_NUM)
            {
                _queueSmoothG.TryDequeue(out var _);
            }

            var smGs = _queueSmoothG.ToArray();
            _gY = smGs.Average(g => g.Y);
            _gX = smGs.Average(g => g.X);
            _g = (float)Math.Sqrt((_gY * _gY) + (_gX * _gX));

            //System.Diagnostics.Debug.WriteLine($"spd={gpss[2].Speed.ToString("F1")}, G={_g.ToString("F1")} Gy={_gY.ToString("F2")}, Gx={_gX.ToString("F2")}");
        }

        private float _gY = 0;
        private float _gX = 0;
        private float _g = 0;

        public float Gy => _gY;
        public float Gx => _gX;
        public float G => _g;
    }
}
