using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DriveApp.GPSLapTimer.Infrastructure
{
    internal class MockReceiverDp3Log : IGpsReceiver
    {
        private Queue<string> _data = new Queue<string>();

        public MockReceiverDp3Log() 
        {
            // LAP+ ログリーダ
            // https://github.com/yoshinrt/vsd/blob/master/vsd_filter/vsd_plugins/_log_reader/dp3.js

            // dp3 フォーマット
            // ビッグエンディアン，0x100 以降が 16バイト/1レコード のログで，
            // +0x00-0x03: 日本時間 0:00 からの時間，日付は失われている  1秒 = 10
            // +0x04-0x07: 経度 1度 = 460800
            // +0x08-0x0B: 緯度
            // +0x0C-0x0D: 時速 1km/h = 10
            // +0x0E-0x0F: 方位，このリーダでは不使用

            var log = @"C:\Users\katsuo\Documents\dp3\NIKKO.dp3";
            //var log = @"C:\Users\katsuo\Documents\dp3\TC2000.dp3";
            //var log = @"C:\Users\katsuo\Documents\dp3\SUZUKA.dp3";
            //var log = @"C:\Users\katsuo\Documents\dp3\SUGO.dp3";
            //var log = @"C:\Users\katsuo\Documents\dp3\EBISU-EAST.dp3";

            using (var fs = new FileStream(log, FileMode.Open, FileAccess.Read))
            {
                byte[] buffer = new byte[16];
                fs.Seek(16 * 16, SeekOrigin.Begin);
                var read = 0;

                while (fs.Read(buffer, 0, buffer.Length) == buffer.Length)
                {
                    byte[] bufTime = new byte[4];
                    byte[] bufLon = new byte[4];
                    byte[] bufLat = new byte[4];
                    byte[] bufSpd = new byte[2];

                    Array.Copy(buffer, 0, bufTime, 0, bufTime.Length);
                    Array.Copy(buffer, 4, bufLon, 0, bufLon.Length);
                    Array.Copy(buffer, 8, bufLat, 0, bufLat.Length);
                    Array.Copy(buffer, 12, bufSpd, 0, bufSpd.Length);

                    long timems = (buffer[0] * 256 * 256 * 256 + buffer[1] * 256 * 256 + buffer[2] * 256 + buffer[3]) * 100;
                    double longitude = (buffer[4] * 256 * 256 * 256 + buffer[5] * 256 * 256 + buffer[6] * 256 + buffer[7]) / 460800.0;
                    double latitude = (buffer[8] * 256 * 256 * 256 + buffer[9] * 256 * 256 + buffer[10] * 256 + buffer[11]) / 460800.0;
                    double speed = (buffer[12] * 256 + buffer[13]) / 10.0;
                    //long timems = BitConverter.ToInt32(bufTime) * 100;
                    //double longitude = BitConverter.ToInt32(bufLon) / 460800.0;
                    //double latitude = BitConverter.ToInt32(bufLat) / 460800.0;
                    //float speed = BitConverter.ToInt16(bufSpd) / 10f;
                    // System.Diagnostics.Debug.WriteLine($"{latitude.ToString("F6")},{longitude.ToString("F6")}");

                    var lat = ((int)latitude * 100) + (latitude - (int)latitude) * 60;
                    var lon = ((int)longitude * 100) + (longitude - (int)longitude) * 60;
                    var not = speed / 1.825;

                    _data.Enqueue(TEMPLATE_LOG
                        .Replace("%LATITUDE%", lat.ToString("F5"))
                        .Replace("%LONGITUDE%", lon.ToString("F5"))
                        .Replace("%SPEED%", not.ToString("F4")));
                }
            }
        }

        private bool _isOpen = false;
        public void Open()
        {
            _isOpen = true;
        }
        public string ReadLine()
        {
            Thread.Sleep(196);
            //Thread.Sleep(50);
            return _data.Dequeue().Replace("%TIME%", DateTime.Now.ToString("HHmmss.fff"));
        }

        public bool IsOpen => _isOpen;

        public void Close() { }

        public void Dispose()
        {
        }
        
        private const string TEMPLATE_LOG = @"$GPRMC,%TIME%,A,%LATITUDE%,N,%LONGITUDE%,E,%SPEED%,172.94,260540,,,A*67";
    }
}
