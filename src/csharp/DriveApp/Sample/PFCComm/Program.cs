using System.IO.Ports;
using System.Text;

Console.WriteLine("使用するシリアルポートのポート番号[3]：");
string comNoStr = string.Empty;
int comNo = 0;

while (!int.TryParse((comNoStr = Console.ReadLine()), out comNo))
{
    if (string.IsNullOrEmpty(comNoStr))
    {
        comNoStr = "3";
        comNo = 3;
        break;
    }
    Console.WriteLine("使用するシリアルポートのポート番号：");
}

string delayMsStr = string.Empty;
int delayMs = 100;

Console.WriteLine("DelayMs[100]：");
while (!int.TryParse((delayMsStr = Console.ReadLine()), out delayMs))
{
    if (string.IsNullOrEmpty(delayMsStr))
    {
        delayMsStr = "100";
        delayMs = 100;
        break;
    }
    Console.WriteLine("DelayMs：");
}

if (delayMs < 50) { delayMs = 50; }

var com = $"COM{comNo}";

var start = $"Port: {com}, Delay: {delayMs}";
Console.WriteLine(start);
Console.WriteLine("Enterで開始します。");
Console.ReadLine();

var running = true;
Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) =>
{
    running = false;
    e.Cancel = true;
};

var logfile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{DateTime.Now.ToString("yyyy-MM-dd-HHmmss")}.log");

using (SerialPort serialPort = new SerialPort(com, 19200, Parity.Even, 8, StopBits.One))
{
    serialPort.ReadTimeout = 1000; // 1s
    serialPort.WriteTimeout = 1000; // 1s

    //_serialPort.DataReceived += _serialPort_DataReceived;
    serialPort.Open();
    serialPort.DiscardOutBuffer();
    serialPort.DiscardInBuffer();


    await Task.Delay(500);
    byte[] init = new byte[] { 0xF3, 0x2, 0xA };
    byte[] cmd = new byte[] { 0xF0, 0x2, 0xD };

    using (FileStream fileStream = new FileStream(logfile, FileMode.Append, FileAccess.Write))
    {
        {
            start += "\r\n";
            var buff = Encoding.UTF8.GetBytes(start);
            fileStream.Write(buff, 0, buff.Length);
        }
        try
        {
            {
                serialPort.Write(init, 0, cmd.Length);

                byte[] receive = new byte[11];


                serialPort.DiscardOutBuffer();
                serialPort.DiscardInBuffer();

                serialPort.Read(receive, 0, receive.Length);

                var txt = $"{DateTime.Now.ToString("HH:mm:ss.fff")}|{BitConverter.ToString(receive)}\r\n";

                Console.Write(txt);

                var buff = Encoding.UTF8.GetBytes(txt);
                fileStream.Write(buff, 0, buff.Length);

                await Task.Delay(1000);
            }


            while (running)
            {
                serialPort.Write(cmd, 0, cmd.Length);

                byte[] receive = new byte[33];
                serialPort.Read(receive, 0, receive.Length);

                var txt = $"{DateTime.Now.ToString("HH:mm:ss.fff")}|{BitConverter.ToString(receive)}\r\n";

                Console.Write(txt);

                var buff = Encoding.UTF8.GetBytes(txt);
                fileStream.Write(buff, 0, buff.Length);

                if (delayMs > 0)
                    await Task.Delay(delayMs);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);

        }
        finally {
            serialPort.Close();
        }
    }
}

Console.WriteLine("キーを押すと終了します。");
Console.ReadLine();
