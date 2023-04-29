using PFC;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Ports;
using System.Text.Json;

var json = File.ReadAllText(@"C:\Users\katsu713b\source\repos\katsu713b\DriveApps\src\csharp\DriveApp\Sample\PFCEmu\AppData\sample.json");
var samples = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

Console.WriteLine("使用するシリアルポートのポート番号[9]：");
string comNoStr = string.Empty;
int comNo = 0;

while (!int.TryParse((comNoStr = Console.ReadLine()), out comNo))
{
    if (string.IsNullOrEmpty(comNoStr))
    {
        comNoStr = "9";
        comNo = 9;
        break;
    }
    Console.WriteLine("使用するシリアルポートのポート番号：");
}


var com = $"COM{comNo}";
var running = true;

Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) =>
{
    running = false;
    e.Cancel = true;
};

Run();

 void Run()
{
    var mstest50 = "2023-04-22-210003.log";
    var run = "2023-04-22-204715.log";

    const string cmdAdvance = "F0-02-0D";
    const string cmdInit = "F3-02-0A";
    const string f40209 = "F4-02-09";
    const string f50208 = "F5-02-08";
    const string AD0250 = "AD-02-50";
    const string b8A0273 = "8A-02-73";

    const int ilen = 1;

    using (StreamReader file = new StreamReader(@$"C:\Users\katsu713b\source\repos\katsu713b\DriveApps\src\csharp\DriveApp\Sample\PFCEmu\AppData\{run}"))
    using (SerialPort serialPort = new SerialPort(com, 19200, Parity.Even, 8, StopBits.One))
    {
        //_serialPort.ReadTimeout = 0; // 1s
        //_serialPort.WriteTimeout = 1000; // 1s

        //_serialPort.DataReceived += _serialPort_DataReceived;
        serialPort.Open();
        serialPort.DiscardInBuffer();
        serialPort.DiscardOutBuffer();

        byte[] cmd = new byte[] { 0xF0, 0x2, 0xD };

        try
        {
            int speed = 0;
            int rpm = 0;
            while (running)
            {
                byte[] buffer = new byte[256];
                Span<byte> span = buffer;

                serialPort.Read(buffer, 0, 2);
                serialPort.Read(buffer, 2, buffer[ilen] - 1);
                
                var txt = BitConverter.ToString(buffer, 0, buffer[ilen] + 1);
                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] R:{txt}");

                switch (txt)
                {
                    case cmdAdvance:

                        if (txt == cmdAdvance)
                        {
                            if (file.EndOfStream)
                                file.BaseStream.Position = 0;


                            var line = file.ReadLine();
                            if (line == null)
                            {
                                continue;
                            }

                            var log = line.Split('-').Select(c => Convert.ToByte(c, 16)).ToArray();

                            serialPort.Write(log, 0, log.Length);
                        }
                        break;
                    default:

                        if (samples.ContainsKey(txt))
                        {
                            var res = Helper.ConvertToByte(samples[txt]);
                            serialPort.Write(res, 0, res.Length);
                        }
                        else
                        {
                            var err = new byte[] { 0xFF, 0x2, 0x0 };
                            serialPort.Write(err, 0, err.Length);
                        }

                        break;
                }
                //await Task.Delay(30);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);

        }
        finally
        {
            // Dispose resource
            //using (accessor){ }

            serialPort.Close();
        }
    }
}

internal static class Helper
{
    public static byte[] ConvertToByte(string byteText)
    {
        return byteText.Split('-').Select(c => Convert.ToByte(c, 16)).ToArray();
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
}