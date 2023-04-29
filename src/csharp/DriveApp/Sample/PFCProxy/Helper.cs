using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PFCProxy
{
    internal static class Helper
    {
        public static bool ChecksumVerification()
        {
            var data = ConvertToByte("B810A41FE8034C04B0045C037003B603FA".AsSpan());

            byte buf = 0;
            for (int i = 0; i < data.Length; i++)
            {
                buf = (byte)(data[i] + buf);
            }

            return buf == 255;
        }

        public static byte[] ConvertToByte(ReadOnlySpan<char> source)
        {
            byte[] byteArray = new byte[source.Length / 2];  // byte配列の初期化

            for (int i = 0; i < source.Length; i += 2)
            {
                byteArray[i / 2] = Convert.ToByte(new String(source.Slice(i, 2)), 16);  // 16進数の文字列をbyteに変換
            }
            return byteArray;
        }

        public static void Write(this FileStream fs, string message)
        {
            var buff = Encoding.UTF8.GetBytes(message);
            fs.Write(buff, 0, buff.Length);
        }
    }
}
