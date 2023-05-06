using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PFCEmu;

internal class Main
{
    public void Run(string com, ref bool running)
    {
        var json = File.ReadAllText(@"C:\pfc\data\sample.json");
        var samples = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

        var run = "2023-04-22-204715.log";

        const string cmdAdvance = "F0-02-0D";
        const string cmdInit = "F3-02-0A";
        const string f40209 = "F4-02-09";
        const string f50208 = "F5-02-08";
        const string AD0250 = "AD-02-50";
        const string b8A0273 = "8A-02-73";

        const int ilen = 1;

        using (StreamReader file = new StreamReader(@$"C:\pfc\data\{run}"))
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
}
