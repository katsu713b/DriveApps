using PFC;
using System.IO.Ports;
using System.Text.Json;

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

Console.WriteLine("Timeout[-1]：");
var timeoutStr = Console.ReadLine();
var timeout = -1;
if (!string.IsNullOrEmpty(timeoutStr))
    timeout = int.Parse(timeoutStr);

Console.WriteLine("Delay[0]：");
var delayStr = Console.ReadLine();
var delay = 0;
if (!string.IsNullOrEmpty(delayStr))
    delay = int.Parse(delayStr);

Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) =>
{
    running = false;
    e.Cancel = true;
};

//const string cmdAdvance = "F0-02-0D";
//const string cmdBasic = "DA-02-23";
//const string cmdInit = "F3-02-0A";
//const string f40209 = "F4-02-09";
//const string f50208 = "F5-02-08";
//const string AD0250 = "AD-02-50";
//const string b8A0273 = "8A-02-73";

byte[] cmdBasic = new byte[] { 0xDA, 0x02, 0x23 };

await CommanderInit();

async Task CommanderInit()
{
    do
    {
        Queue<byte[]> queue = new Queue<byte[]>(new[] {
            new byte[] { 0xD7, 0x2, 0x26 },
            new byte[] { 0xD8, 0x2, 0x25 },
            new byte[] { 0xD9, 0x2, 0x24 },
            new byte[] { 0xCA, 0x2, 0x33 },
            new byte[] { 0xF3, 0x2, 0x0A },
            new byte[] { 0xF5, 0x2, 0x08 },
            new byte[] { 0xF9, 0x2, 0x04 }
        });

        using (SerialPort serialPort = new SerialPort())
        {
            Console.WriteLine("Start.");

            serialPort.SetUpPFC(com, timeout, timeout);

            serialPort.Open();
            serialPort.DiscardInBuffer();
            serialPort.DiscardOutBuffer();

            while (queue.TryDequeue(out var cmd))
            {
                WriteCmd(serialPort, cmd);
                if (!running) return;

                if (delay > 0) { await Task.Delay(delay); }

                var res = serialPort.Read();
                Helper.WriteR(res);

            }

            var count = 0;
            while (running)
            {
                if (!running) return;
                if (count++ > 5) break;

                WriteCmd(serialPort, cmdBasic);
                var res = serialPort.Read();
                Helper.WriteR(res);

                await Task.Delay(200);
            }
        }

        Console.WriteLine("End.");

        Console.WriteLine("[Enter]で再実行します");

    }
    while (string.IsNullOrEmpty(Console.ReadLine()));
}


bool WriteCmd(SerialPort sp, byte[] cmd)
{
    for (int i = 0; i < 2 + 1; i++)
    {
        if (!running) return false;

        try
        {
            Helper.WriteW(cmd);
            sp.Write(cmd, 0, cmd.Length);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    return false;
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

    public static void WriteR(ReadOnlySpan<byte> data)
    {
        var tmp = data.ToArray();
        var txt = $"[{DateTime.Now.ToString("HH:mm:ss.fff")}] R:{BitConverter.ToString(tmp)}\n";
        Console.Write(txt);
    }
    public static void WriteW(ReadOnlySpan<byte> data)
    {
        var tmp = data.ToArray();
        var txt = $"[{DateTime.Now.ToString("HH:mm:ss.fff")}] W:{BitConverter.ToString(tmp)}\n";
        Console.Write(txt);
    }
}
