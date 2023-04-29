using PFCProxy;
using System.IO.Ports;


Console.WriteLine("入力用シリアルポートのポート番号[9]：");
string comNoStr = Console.ReadLine();
if (string.IsNullOrEmpty(comNoStr))
    comNoStr = "9";

var comIn = $"COM{comNoStr}";

Console.WriteLine("出力用シリアルポートのポート番号[3]：");
comNoStr = Console.ReadLine();
if (string.IsNullOrEmpty(comNoStr))
    comNoStr = "3";

var comOut = $"COM{comNoStr}";


var running = true;

Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) =>
{
    running = false;
    e.Cancel = true;
};

Run();


void Run()
{

    var logfile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{DateTime.Now.ToString("Proxy_yyyy-MM-dd-HHmmss")}.log");

    using (SerialPort serialIn = new SerialPort(comIn, 19200, Parity.Even, 8, StopBits.One))
    using (SerialPort serialOut = new SerialPort(comOut, 19200, Parity.Even, 8, StopBits.One))
    {
        //_serialPort.ReadTimeout = 0; // 1s
        //_serialPort.WriteTimeout = 1000; // 1s

        //_serialPort.DataReceived += _serialPort_DataReceived;
        serialIn.Open();
        serialOut.Open();

        byte[] cmd = new byte[] { 0xF0, 0x2, 0xD };

        serialIn.DiscardInBuffer();
        serialIn.DiscardOutBuffer();

        serialOut.DiscardInBuffer();
        serialOut.DiscardOutBuffer();

        FileStream fileStream = new FileStream(logfile, FileMode.Append, FileAccess.Write);
        const int icmd = 0;
        const int ilen = 1;
        try
        {
            byte[] buffer = new byte[256];
            Span<byte> span = buffer;

            while (running)
            {
                // Soft/Commander -> PFC
                serialIn.Read(buffer, 0, 2);
                serialIn.Read(buffer, 2, buffer[ilen] - 1);

                serialOut.DiscardInBuffer();
                serialOut.Write(buffer, 0, buffer[ilen] + 1);

                WriteR(fileStream, span[..span[ilen]]);

                // PFC -> Soft
                serialOut.Read(buffer, 0, 2);
                serialOut.Read(buffer, 2, buffer[ilen] - 1);

                serialIn.DiscardInBuffer();
                serialIn.Write(buffer, 0, buffer[ilen] + 1);

                WriteW(fileStream, span[..span[ilen]]);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            using (fileStream) { }
            serialIn.Close();
            serialOut.Close();
        }
    }

    void WriteR(FileStream sm, Span<byte> data)
    {
        var tmp = data.ToArray();
        var txt = $"[{DateTime.Now.ToString("HH:mm:ss.fff")}] R:{BitConverter.ToString(tmp)}\n";
        Console.Write(txt);
        sm.Write(txt);
    }
    void WriteW(FileStream sm, Span<byte> data)
    {
        var tmp = data.ToArray();
        var txt = $"[{DateTime.Now.ToString("HH:mm:ss.fff")}] W:{BitConverter.ToString(tmp)}\n";
        Console.Write(txt);
        sm.Write(txt);
    }
}
