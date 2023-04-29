using System.IO.Ports;
using System.Reflection.Metadata.Ecma335;
using System.Text;




var q = new Queue<int>();

q.Enqueue(1);
q.Enqueue(2);
q.Enqueue(3);
q.Enqueue(4);
q.Enqueue(5);

while (q.Peek() < 3)
{
    
    q.Dequeue();
}

q.Enqueue(6);


var a = new byte[] { 1, 2, 3 };

void aa(byte[] aa)
{
    aa[0] = 0;
}
void bb(Span<byte> bb)
{
    bb[1] = 0;
    var cc = bb.ToArray();
    bb[2] = 0;
}

aa(a);

bb(a);



Console.WriteLine("使用するシリアルポートのポート番号：");
var comNoStr = Console.ReadLine();
int comNo;
while (string.IsNullOrEmpty(comNoStr) || !int.TryParse(comNoStr, out comNo))
{
    Console.WriteLine("使用するシリアルポートのポート番号：");
    comNoStr = Console.ReadLine();
}

var com = $"COM{comNo}";
var running = true;
Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) =>
{
    running = false;
    e.Cancel = true;
};

var logfile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"gps_{DateTime.Now.ToString("yyyy-MM-dd-HHmmss")}.log");
//
using (SerialPort serialPort = new SerialPort(com, 115200))
using (FileStream fs = new FileStream(logfile, FileMode.Append, FileAccess.Write, FileShare.Read))
{
    serialPort.Open();

    var comma = Encoding.UTF8.GetBytes(",");
    var crlf = Encoding.UTF8.GetBytes("\r\n");
    try
    {
        while (running)
        {
            var receive = serialPort.ReadLine();
            fs.Write(Encoding.UTF8.GetBytes(DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString()));
            fs.Write(comma);
            fs.Write(Encoding.UTF8.GetBytes(receive));
            Console.WriteLine(receive);
        }
    }
    catch (Exception e)
    {
        Console.WriteLine(e);

    }
}


Console.WriteLine("キーを押すと終了します。");
Console.ReadLine();