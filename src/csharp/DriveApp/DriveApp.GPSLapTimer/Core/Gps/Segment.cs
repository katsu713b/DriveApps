using System.Numerics;

namespace DriveApp.GPSLapTimer.Core.Gps
{
    internal struct Segment
    {
        public GeoPoint StartPoint { get; }
        //public Vector2 Vector { get; }
        public Vector3 Vector { get; }

        public Segment(GeoPoint start, GeoPoint end)
        {
            StartPoint = start;
            Vector = start.CreateV3FromGeo(end);
        }
    }
}
