using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("GPSLapTimerTest")]

namespace DriveApp.GPSLapTimer.Core.Gps
{

    /*
    | 単語例	    | 説明	                                                                               | 意味
    | 085120.307	| 協定世界時(UTC）での時刻。日本標準時は協定世界時より9時間進んでいる。hhmmss.ss       | UTC時刻：08時51分20秒307
    | A	            | ステータス。V = 警告、A = 有効                                                       | ステータス：有効
    | 3541.1493     | 緯度。dddmm.mmmm                                                                     | 緯度：35度41.1493分
    |               | 60分で1度なので、分数を60で割ると度数になります。Googleマップ等で用いられる          | 
    |               | ddd.dddd度表記は、(度数 + 分数/60) で得ることができます。	                           | 
    | N	            | 北緯か南緯か。N = 北緯、South = 南緯	                                               | 北緯
    | 13945.3994	| 経度  dddmm.mmmm                                                                     | 経度；139度45.3994分
    |               | 60分で1度なので、分数を60で割ると度数になります。                                    | 
    |               | Googleマップ等で用いられる ddd.dddd度表記は、(度数 + 分数/60) で得ることができます。 | 
    | E	            | 東経か西経か。E = 東経、West = 西経                                                  | 東経
    | 000.0	        | 地表における移動の速度。000.0～999.9[knot]	移動の速度：000.0[knot]
    | 240.3	        | 地表における移動の真方位。000.0～359.9度	移動の真方位：240.3度
    | 181211	    | 協定世界時(UTC）での日付。ddmmyy	UTC日付：2011年12月18日
    | _             | 磁北と真北の間の角度の差。000.0～359.9度	
    | _             | 磁北と真北の間の角度の差の方向。E = 東、W = 西	
    | A	            | モード, N = データなし, A = Autonomous（自律方式）, D = Differential（干渉測位方式）, E = Estimated（推定）	モード：自律方式
    | *6A	        | チェックサム	チェックサム値：6A

    $GPRMC,120001.000,A,3639.7526,N,13951.7884,E,0.19,172.94,260540,,,A*68
    $GPRMC,120001.200,A,3639.7527,N,13951.7842,E,0.19,172.94,260540,,,A*69
    $GPRMC,120001.400,A,3639.7522,N,13951.7803,E,0.19,172.94,260540,,,A*6A
    $GPRMC,120001.600,A,3639.7512,N,13951.7752,E,0.19,172.94,260540,,,A*63
    $GPRMC,120001.800,A,3639.7484,N,13951.7689,E,0.19,172.94,260540,,,A*68
    $GPRMC,120002.000,A,3639.7443,N,13951.7624,E,0.19,172.94,260540,,,A*65
     */

    internal interface IGpsValue
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="Latitude">緯度</param>
    /// <param name="Longitude">経度</param>
    internal record GeoPoint(double Latitude, double Longitude);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="RawText"></param>
    /// <param name="UtcTime"></param>
    /// <param name="Geo"></param>
    /// <param name="TrueBearing"></param>
    /// <param name="Speed"></param>
    /// <param name="IsValid"></param>
    internal record GpsValue(string RawText, DateTime UtcTime, GeoPoint Geo, double TrueBearing, float Speed, bool IsValid, Segment Seg) : IGpsValue
    {
        public DateTime LocalTime => UtcTime.ToLocalTime();
    }

    internal static class GpsValueFactory
    {
        private static readonly GpsValue _nodata = new GpsValue("", DateTime.Now, new GeoPoint(0, 0), 0, 0, false, new Segment(new GeoPoint(0, 0), new GeoPoint(0, 0)));

        public static GpsValue CreateValue(string raw, GeoPoint from)
        {
            if (string.IsNullOrEmpty(raw)) return _nodata;
            var values = raw.Split(',');

            if (values[0] != "$GPRMC") throw new ArgumentException("GPRMC support only ");
            if (values[12] == "N") return _nodata;

            var date = new DateTime(
                2000 + int.Parse(values[9].Substring(4, 2)),
                int.Parse(values[9].Substring(2, 2)),
                int.Parse(values[9].Substring(0, 2)),
                int.Parse(values[1].Substring(0, 2)),
                int.Parse(values[1].Substring(2, 2)),
                int.Parse(values[1].Substring(4, 2)),
                int.Parse(values[1].Substring(7, 3)),
                DateTimeKind.Utc);
            var lat = ConvertToDegress(values[3]);
            var lon = ConvertToDegress(values[5]);
            float speed = float.Parse(values[7]) * 1.825f;
            var trueBearing = double.Parse(values[8]);
            var geo = new GeoPoint(lat, lon);
            var seg = new Segment(from ?? geo, geo);
            return new GpsValue(raw, date, geo, trueBearing, speed, true, seg);
        }

        private static double ConvertToDegress(string raw)
        {
            var rawm = double.Parse(raw);

            return (int)(rawm / 100) + (rawm % 100) / 60;
        }
    }
}
