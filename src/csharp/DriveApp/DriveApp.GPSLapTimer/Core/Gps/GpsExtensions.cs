using System;
using System.Numerics;

namespace DriveApp.GPSLapTimer.Core.Gps
{
    internal static class GeoPointExtension
    {
        public static Vector3 CreateV3FromGeo(this GeoPoint start, GeoPoint end)
        {
            var x = end.Longitude - start.Longitude;
            var y = end.Latitude - start.Latitude;

            return new Vector3((float)x, (float)y, 0);
        }

        const double UPPER_LIMIT_RADIUS_METER = 5000;
        public static double RadiusMeters(this GeoPoint p1, GeoPoint p2, GeoPoint p3)
        {
            var center = CenterPointOrNull(p1, p2, p3);
            if (center == null)
            {
                return UPPER_LIMIT_RADIUS_METER;
            }

            return Math.Min(center.DistanceFrom(p1), UPPER_LIMIT_RADIUS_METER);
        }

        /// <summary>
        /// 3点を通る円の中心座標
        /// </summary>
        /// <remarks>
        /// https://www.mynote-jp.com/entry/Circle-Defined-by-Three-Points
        /// </remarks>
        public static GeoPoint CenterPointOrNull(this GeoPoint p1, GeoPoint p2, GeoPoint p3)
        {
            var a = p1.Latitude - p2.Latitude;
            var b = p1.Longitude - p2.Longitude;
            var c = p2.Latitude - p3.Latitude;
            var d = p2.Longitude - p3.Longitude;
            var l1 = (p1.Latitude * p1.Latitude + p1.Longitude * p1.Longitude) - (p2.Latitude * p2.Latitude + p2.Longitude * p2.Longitude);
            var l2 = (p2.Latitude * p2.Latitude + p2.Longitude * p2.Longitude) - (p3.Latitude * p3.Latitude + p3.Longitude * p3.Longitude);
            var x = 2 * (a * d - b * c);
            if (x == 0) return null;
            var lat = (d * l1 - b * l2) / x;
            var lon = (-1 * c * l1 + a * l2) / x;
            return new GeoPoint(lat, lon);
        }

        private const double TO_RADIAN = Math.PI / 180;

        public static double DistanceFrom(this GeoPoint to, GeoPoint from)
        {
            // ヒュベニの公式
            const double a = 6378137.000;
            const double b = 6356752.314245;
            const double e2 = (a * a - b * b) / (a * a);

            double dx = (to.Longitude - from.Longitude) * TO_RADIAN;
            double dy = (to.Latitude - from.Latitude) * TO_RADIAN;
            double uy = (from.Latitude + to.Latitude) / 2 * TO_RADIAN;
            double W = Math.Sqrt(1 - e2 * Math.Sin(uy) * Math.Sin(uy));
            double M = a * (1 - e2) / Math.Pow(W, 3);
            double N = a / W;

            return Math.Sqrt(dy * dy * M * M + Math.Pow(dx * N * Math.Cos(uy), 2));
        }
    }

    internal static class GpsExtensions
    {
        private const float TO_GRAVITY = 9.80665f;
        private const double TO_RADIAN = Math.PI / 180;

        /// <summary>
        /// 前後加速度
        /// </summary>
        /// <param name="v">速度(km/h)</param>
        /// <param name="v0">初速(km/h)</param>
        /// <param name="ms">経過時間(ms)</param>
        /// <returns></returns>
        public static float Gy(this GpsValue current, GpsValue before, int ms)
        {
            // v＝v0＋at
            // v0: 初速(m/s)
            // v: 加速した後の速度(m/s)
            // a: 加速度(m/s^2)
            // t: 経過時間(s)

            // a = (v - v0) / t
            return (current.Speed - before.Speed) * 1000 / 3.6f / ms / TO_GRAVITY;
        }

        /// <summary>
        /// 遠心加速度
        /// </summary>
        /// <param name="v">速度(km/h)</param>
        /// <returns></returns>
        public static float Gx(this GpsValue current, GeoPoint p2, GeoPoint p3, double smoothR)
        {
            const int LIMIT_RADIUS_M = 3000;
            var r = current.Geo.RadiusMeters(p2, p3);

            if (r >= LIMIT_RADIUS_M) return 0;
            if (r < 4) return 0; // 停車中扱い
            if (smoothR >= LIMIT_RADIUS_M) return 0;

            var direction = Math.Atan2(current.Geo.Latitude - p2.Latitude, current.Geo.Longitude - p2.Longitude) / TO_RADIAN + 90;
            var directionBefore = Math.Atan2(p2.Latitude - p3.Latitude, p2.Longitude - p3.Longitude) / TO_RADIAN + 90;

            // a=v^2/r
            // v: 速度(m/s)

            var gy = (float)((current.Speed * current.Speed / 12.96) / Math.Max(r, smoothR) / TO_GRAVITY);
            if (direction - directionBefore > 0)
                return gy;

            return -gy;
        }
    }
}
