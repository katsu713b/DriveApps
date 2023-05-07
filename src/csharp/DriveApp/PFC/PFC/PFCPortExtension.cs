using System.IO.Ports;

namespace PFC;

public static class PFCPortExtension
{
    public static SerialPort CreatePFCPort(string name, int readTimeout = -1, int writeTimeout = -1)
    {
        SerialPort port = new SerialPort();
        port.SetUpPFC(name, readTimeout, writeTimeout);
        return port;
    }

    public static void SetUpPFC(this SerialPort port, string? name, int readTimeout = -1, int writeTimeout = -1)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

        port.PortName = name;
        port.BaudRate = 19200;
        port.Parity = Parity.Even;
        port.DataBits = 8;
        port.StopBits = StopBits.One;
        port.ReadTimeout = readTimeout;
        port.WriteTimeout = writeTimeout;
    }

    private static readonly byte[] EmptyData = Enumerable.Empty<byte>().ToArray();

    public static byte[] Read(this SerialPort sp)
    {
         var buff = new byte[256];
        try
        {
            var waitMs = 0;
            if (sp.ReadTimeout > 0)
            {
                while (sp.BytesToRead == 0 && waitMs < 90)
                {
                    Thread.Sleep(15);
                    waitMs += 15;
                }
            }

            // buff[1] + 1 == DataLength
            var len = 0;
            while (len == 0)
            {
                var tmp = sp.Read(buff, 0, 1);
                if (tmp == 0)
                {
                    Thread.Sleep(10);
                    continue;
                }
                len += tmp;
            }

            if (buff[0] <= 0x10 || buff[0] == 0xFF)
            {
                //Console.WriteLine($"if (buff[0] == 0 || buff[0] == 0xFF), buff[0]:{buff[0]}, len={len}");
                return EmptyData;
            }

            while (len < 3)
            {
                var tmp = sp.Read(buff, 1, 2);
                if (tmp == 0)
                {
                    Thread.Sleep(10);
                    continue;
                }
                len += tmp;
            }

            if (buff[1] == 0)
            {
                //Console.WriteLine($"if (buff[1] == 0), buff:{BitConverter.ToString(buff, 0, 6)}, len={len}");
                return EmptyData;
            }

            if (buff[1] == 2) return buff.AsSpan(0, 3).ToArray();

            var totalLen = buff[1] + 1;
            while (sp.BytesToRead < totalLen - 3)
            {
                Thread.Sleep(10);
            }
            while (len < totalLen)
            {
                var tmp = sp.Read(buff, 3, totalLen - len);
                if (tmp == 0)
                {
                    Thread.Sleep(10);
                    continue;
                }
                len += tmp;
            }

            ReadOnlySpan<byte> data = buff.AsSpan(0, buff[1] + 1);
            if (!ResponseBase.ChecksumVerification(data))
            {
                //Console.WriteLine($"if (!ResponseBase.ChecksumVerification(data))");
                return EmptyData;
            }

            return data.ToArray();
        }
        catch (TimeoutException to)
        {
            //Console.WriteLine($"Timeout");
            return EmptyData;
        }
        catch
        {
            //Console.WriteLine(e);
            return EmptyData;
        }
    }

    public static byte[] Read0(this SerialPort sp, bool pfc = false)
    {
        var buff = new byte[256];
        try
        {
            // buff[1] + 1 == DataLength
            var len = 0;
            while (len < 3)
            {
                var tmp = sp.Read(buff, len, 3 - len);
                if (tmp == 0)
                {
                    Thread.Sleep(10);
                    continue;
                }
                len += tmp;
            }

            if (buff[0] <= 0x10 || buff[0] == 0xFF)
            {
                //Console.WriteLine($"if (buff[0] == 0 || buff[0] == 0xFF), buff[0]:{buff[0]}, len={len}");
                return EmptyData;
            }
            if (buff[1] == 0)
            {
                //Console.WriteLine($"if (buff[1] == 0), buff:{BitConverter.ToString(buff, 0, 6)}, len={len}");
                return EmptyData;
            }
            if (buff[1] == 2) return buff.AsSpan(0, 3).ToArray();
            
            var totalLen = buff[1] + 1;
            while (sp.BytesToRead < totalLen-3)
            {
                Thread.Sleep(10);
            }
            while (len < totalLen)
            {
                var tmp = sp.Read(buff, 3, totalLen - len);
                if (tmp == 0)
                {
                    Thread.Sleep(10);
                    continue;
                }
                len += tmp;
            }

            ReadOnlySpan<byte> data = buff.AsSpan(0, buff[1] + 1);
            if (!ResponseBase.ChecksumVerification(data))
            {
                //Console.WriteLine($"if (!ResponseBase.ChecksumVerification(data))");
                return EmptyData;
            }

            return data.ToArray();
        }
        catch (TimeoutException to)
        {
            //Console.WriteLine($"Timeout");
            return EmptyData;
        }
        catch
        {
            //Console.WriteLine(e);
            return EmptyData;
        }
    }
}
