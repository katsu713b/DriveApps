using PFCProxy;
using System.IO.Ports;
using PFC;
using System.Data;
using System;
using System.IO;

foreach (var name in SerialPort.GetPortNames())
{
    Console.WriteLine(name);
}

Console.WriteLine("入力用シリアルポートのポート番号[5]：");
string? comNoStr = Console.ReadLine();
if (string.IsNullOrEmpty(comNoStr))
    comNoStr = "5";

var comIn = $"COM{comNoStr}";

Console.WriteLine("出力用シリアルポートのポート番号[3]：");
comNoStr = Console.ReadLine();
if (string.IsNullOrEmpty(comNoStr))
    comNoStr = "3";

var comOut = $"COM{comNoStr}";


Console.WriteLine("Timeout[200]：");
var timeoutStr = Console.ReadLine();
var timeout = 200;
if (!string.IsNullOrEmpty(timeoutStr))
    timeout = int.Parse(timeoutStr);

var running = true;

Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) =>
{
    running = false;
    e.Cancel = true;
};



var logfile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{DateTime.Now.ToString("Proxy_yyyy-MM-dd-HHmmss")}.log");

using (SerialPort serialIn = new SerialPort())
using (SerialPort serialOut = new SerialPort())
{
    serialIn.SetUpPFC(comIn, 400, 400);
    serialOut.SetUpPFC(comOut, timeout, timeout);

    //_serialPort.ReadTimeout = 0; // 1s
    //_serialPort.WriteTimeout = 1000; // 1s

    //_serialPort.DataReceived += _serialPort_DataReceived;
    serialIn.Open();
    serialOut.Open();

    //byte[] cmd = new byte[] { 0xF0, 0x2, 0xD };


    using (FileStream fileStream = new FileStream(logfile, FileMode.Append, FileAccess.Write))
    {

        while (running)
        {
            serialIn.DiscardInBuffer();
            serialIn.DiscardOutBuffer();

            serialOut.DiscardInBuffer();
            serialOut.DiscardOutBuffer();
            //await Task.Delay(1000);
            try
            {
                Console.WriteLine("Start.");

                while (running)
                {
                    // Soft/Commander -> Proxy
                    var cmd = serialIn.Read();
                    if (cmd.Length == 0) continue;

                    WriteR(fileStream, cmd);

                    // Proxy -> PFC
                    serialOut.DiscardInBuffer();
                    serialOut.Write(cmd, 0, cmd.Length);
                    //await Task.Delay(100);

                    // PFC -> Proxy
                    var res = serialOut.Read(true);
                    if (res.Length == 0) continue;

                    // Proxy -> Soft/Commander
                    WriteW(fileStream, res);
                    serialIn.DiscardInBuffer();
                    serialIn.Write(res, 0, res.Length);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
            }
        }
    }

}

void WriteR(FileStream sm, ReadOnlySpan<byte> data)
{
    var tmp = data.ToArray();
    var txt = $"[{DateTime.Now.ToString("HH:mm:ss.fff")}] R:{BitConverter.ToString(tmp)}\n";
    Console.Write(txt);
    sm.Write(txt);
}
void WriteW(FileStream sm, ReadOnlySpan<byte> data)
{
    var tmp = data.ToArray();
    var txt = $"[{DateTime.Now.ToString("HH:mm:ss.fff")}] W:{BitConverter.ToString(tmp)}\n";
    Console.Write(txt);
    sm.Write(txt);
}