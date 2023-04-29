using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace GPSReceiver
{

    internal class SystemTIme
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct SystemTime
        {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDayOfWeek;
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMiliseconds;
        }

        [DllImport("kernel32.dll")]
        public static extern bool SetLocalTime(
            ref SystemTime sysTime);
        
        /// <summary>
        /// 現在のシステム日時を設定する
        /// </summary>
        /// <param name="dt">設定する日時</param>
        public static void SetNowDateTime(DateTime dt)
        {
            //システム日時に設定する日時を指定する
            SystemTime sysTime = new SystemTime();
            sysTime.wYear = (ushort)dt.Year;
            sysTime.wMonth = (ushort)dt.Month;
            sysTime.wDay = (ushort)dt.Day;
            sysTime.wHour = (ushort)dt.Hour;
            sysTime.wMinute = (ushort)dt.Minute;
            sysTime.wSecond = (ushort)dt.Second;
            sysTime.wMiliseconds = (ushort)dt.Millisecond;
            //システム日時を設定する
            SetLocalTime(ref sysTime);
        }
    }
}
