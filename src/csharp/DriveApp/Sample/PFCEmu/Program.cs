using PFC;
using System.IO.Ports;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;

Console.WriteLine("使用するシリアルポートのポート番号[4]：");
string? comNoStr = string.Empty;
int comNo = 0;

while (!int.TryParse((comNoStr = Console.ReadLine()), out comNo))
{
    if (string.IsNullOrEmpty(comNoStr))
    {
        comNoStr = "4";
        comNo = 4;
        break;
    }
    Console.WriteLine("使用するシリアルポートのポート番号：");
}


var com = $"COM{comNo}";
var running = true;

Console.WriteLine("Timeout[300]：");
var timeoutStr = Console.ReadLine();
var timeout = 300;
if (!string.IsNullOrEmpty(timeoutStr))
    timeout = int.Parse(timeoutStr);

Console.WriteLine("Delay[0]：");
var delayStr = Console.ReadLine();
var delay = 0;
if (!string.IsNullOrEmpty(delayStr))
    delay = int.Parse(delayStr);

Console.WriteLine("Start.");

Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) =>
{
    running = false;
    e.Cancel = true;
};

const string cmdAdvance = "F0-02-0D";
const string cmdBasic = "DA-02-23";
//const string cmdInit = "F3-02-0A";
//const string f40209 = "F4-02-09";
//const string f50208 = "F5-02-08";
//const string AD0250 = "AD-02-50";
//const string b8A0273 = "8A-02-73";

//var mstest50 = "2023-04-22-210003.log";
var run = "2023-04-22-204715.log";

var json = File.ReadAllText(@"C:\DriveApp\bin\pfc\data\sample.json");
var samples = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
if (samples == null)
{
    Console.WriteLine("empty sample.json");
    return;
}

using (StreamReader file = new StreamReader(@$"C:\DriveApp\bin\pfc\data\{run}"))
using (SerialPort serialPort = new SerialPort())
{
    serialPort.SetUpPFC(com, timeout, timeout);
    //_serialPort.ReadTimeout = 0; // 1s
    //_serialPort.WriteTimeout = 1000; // 1s

    //serialPort.ReceivedBytesThreshold = 3;
    //serialPort.DataReceived += serialPort_DataReceived;

    //byte[] cmd0 = new byte[] { 0xF0, 0x2, 0xD };

    serialPort.Open();
    serialPort.DiscardInBuffer();
    serialPort.DiscardOutBuffer();
    byte[] cmd = new byte[1];

    string readLog()
    {
        var line = string.Empty;

        while (string.IsNullOrEmpty(line))
        {
            if (file.EndOfStream)
                file.BaseStream.Position = 0;
            line = file.ReadLine();
        }

        return line;
    }

    while (running)
    {
        string res = string.Empty;
        try
        {
            cmd = serialPort.Read();
            if (cmd.Length == 0)
            {
                if (delay > 0)
                    await Task.Delay(delay);
                continue;
            }

            var txt = BitConverter.ToString(cmd, 0, cmd.Length);
            if (txt != cmdAdvance && txt != cmdBasic)
                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] R:{txt}");
            switch (txt)
            {
                case cmdAdvance:
                case cmdBasic:

                    var line = readLog();

                    var log = line.Split('-').Select(c => Convert.ToByte(c, 16)).ToArray();

                    if (txt == cmdAdvance)
                    {
                        //var txtlog = BitConverter.ToString(log, 0, log.Length);
                        //Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] W:{txtlog}");
                        if (delay > 0)
                            await Task.Delay(delay);
                        res = line;
                        serialPort.Write(log, 0, log.Length);
                        Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] W:{res}");
                    }
                    else if (txt == cmdBasic)
                    {
                        var basic = new AdvancedData(0, log).ConvertToBasicRaw();

                        if (delay > 0)
                            await Task.Delay(delay);

                        res = BitConverter.ToString(basic, 0, basic.Length);

                        serialPort.Write(basic, 0, basic.Length);
                        Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] W:{res}");

                    }
                    break;
                default:

                    if (samples.ContainsKey(txt))
                    {
                        if (delay > 0)
                            await Task.Delay(delay);
                        res = samples[txt];
                        var resbyte = Helper.ConvertToByte(samples[txt]);
                        serialPort.Write(resbyte, 0, resbyte.Length);
                        Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] W:{res}");
                    }
                    else
                    {
                        if (delay > 0)
                            await Task.Delay(delay);
                        var err = new byte[] { 0xFE, 0x2, 0x33 };
                        res = BitConverter.ToString(err, 0, err.Length);

                        serialPort.Write(err, 0, err.Length);
                        Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] W:{res}");
                    }

                    break;
            }
        }
        catch (TimeoutException to)
        {
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] TIMEOUT0:{res}");
        }
        catch (Exception e)
        {
            var txt = BitConverter.ToString(cmd, 0, cmd.Length);
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] ERR:{txt}");
            Console.WriteLine(e);
            if (delay > 0)
                await Task.Delay(delay);
        }
        finally
        {

        }
    }
}


//async Task Run2()
//{
//    var json = File.ReadAllText(@"C:\Users\katsu713b\source\repos\katsu713b\DriveApps\src\csharp\DriveApp\Sample\PFCEmu\AppData\sample.json");
//    //var json = File.ReadAllText(@"C:\DriveApp\bin\pfc\data\sample.json");
//    var samples = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

//    const int ilen = 1;

//    using (StreamReader file = new StreamReader(@$"C:\Users\katsu713b\source\repos\katsu713b\DriveApps\src\csharp\DriveApp\Sample\PFCEmu\AppData\{run}"))
//    //using (StreamReader file = new StreamReader(@"C:\DriveApp\bin\pfc\data\2023-04-22-204715.log"))
//    using (SerialPort serialPort = new SerialPort(com, 19200, Parity.Even, 8, StopBits.One))
//    {
//        //_serialPort.ReadTimeout = 0; // 1s
//        //_serialPort.WriteTimeout = 1000; // 1s

//        serialPort.ReceivedBytesThreshold = 3;
//        serialPort.DataReceived += serialPort_DataReceived;

//        serialPort.Open();
//        serialPort.DiscardInBuffer();
//        serialPort.DiscardOutBuffer();

//        byte[] cmd = new byte[] { 0xF0, 0x2, 0xD };

//        byte[] buffer = new byte[256];

//        try
//        {
//            int speed = 0;
//            int rpm = 0;
//            while (running)
//            {
//                await Task.Delay(1000);
//            }
//        }
//        catch (Exception e)
//        {
//            Console.WriteLine(e);

//        }
//        finally
//        {
//            serialPort.Close();
//            using (serialPort) { }
//        }
//    }
//}

//void serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
//{
//    var buffer = new byte[256];
//    var sp = (SerialPort)sender;
//    var n = sp.Read(buffer, 0, sp.BytesToRead);

//    var txt = BitConverter.ToString(buffer, 0, n);
//    Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] R:{txt}");
//}

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
