using System;
using System.IO.Ports;

namespace DriveApp.GPSLapTimer.Infrastructure
{
    internal interface IGpsReceiver : IDisposable
    {
        public void Open();

        public string ReadLine();

        public bool IsOpen { get; }

        public void Close();
    }

    internal class GpsReceiver : IGpsReceiver
    {
        private SerialPort _serialPort;
        public GpsReceiver(SerialPort serialPort) 
        {
            _serialPort = serialPort;
        }

        public bool IsOpen => _serialPort.IsOpen;

        public void Close()
        {
            if (_serialPort.IsOpen) 
                _serialPort.Close();
        }

        public void Dispose()
        {
            Close();
        }

        public void Open()
        {
            _serialPort.Open();
        }

        public string ReadLine() => _serialPort.ReadLine();
    }

    /// <summary>
    /// $GPRMC
    /// </summary>
    /// <param name="UtcTime"></param>
    /// <param name="Status"></param>
    /// <param name="Latitude"></param>
    /// <param name="Ns"></param>
    /// <param name="Longitude"></param>
    /// <param name="Ew"></param>
    /// <param name="Knot"></param>
    /// <param name="Bearing"></param>
    /// <param name="UtcDate"></param>
    /// <param name="Mode"></param>
    /// <param name="CheckSum"></param>
    internal record Gprmc(string UtcTime, string Status, decimal Latitude, string Ns, decimal Longitude, string Ew,
        decimal Knot, decimal Bearing, string UtcDate, string Mode, string CheckSum )
    {

    }

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
    */
}
