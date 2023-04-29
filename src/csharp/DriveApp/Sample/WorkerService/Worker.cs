using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Runtime.Caching;

namespace WorkerService
{    // ConsoleAPI�N���X
    public sealed class ConsoleAPI
    {
        // https://docs.microsoft.com/en-us/windows/console/handlerroutine
        public delegate bool ConsoleCtrlDelegate(CtrlTypes CtrlType);

        // https://docs.microsoft.com/en-us/windows/console/handlerroutine
        public enum CtrlTypes : uint
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        // https://docs.microsoft.com/en-us/windows/console/setconsolectrlhandler
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate handlerRoutine, bool add);
    }

    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }
        // �R���g���[���n���h��
        private bool ControlHandler(ConsoleAPI.CtrlTypes ctrlType)
        {
            Console.WriteLine(ctrlType);
            switch (ctrlType)
            {
                case ConsoleAPI.CtrlTypes.CTRL_C_EVENT:
                    Console.WriteLine("Ctrl+C��������܂���");
                    // �L�����Z��
                    return true;
                case ConsoleAPI.CtrlTypes.CTRL_BREAK_EVENT:
                    Console.WriteLine("Ctrl+Break��������܂���");
                    // �L�����Z��
                    return true;
                case ConsoleAPI.CtrlTypes.CTRL_CLOSE_EVENT:
                    // .NET��user32.dll/gdi32.dll�����[�h����邽��LOGOFF/SHUTDOWN�͗��p�s��
                    //case ConsoleAPI.CtrlTypes.CTRL_LOGOFF_EVENT:
                    //case ConsoleAPI.CtrlTypes.CTRL_SHUTDOWN_EVENT:
                    // �_�~�[����
                    Console.WriteLine("�I�����Ă��܂�...");
                    this.Dispose();
                    Thread.Sleep(3000);
                    return true;
            }
            return false;
        }
        private FileStream _writer;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // �R���g���[���n���h���ݒ�
            ConsoleAPI.SetConsoleCtrlHandler(ControlHandler, true);



            var path = @"C:\Users\katsu713b\source\repos\katsu713b\DriveApps\src\csharp\DriveApp\Sample\WorkerService\bin\Debug";
            var number = 1;

            MemoryCache c = MemoryCache.Default;
            var writer = new FileStream(Path.Combine(path, $"test_{number}.txt"), FileMode.Append, FileAccess.Write, FileShare.Read, 1024 * 128, true);

            c.Add("writer", writer, new CacheItemPolicy()
            {
                SlidingExpiration = new TimeSpan(1, 0, 0),
                RemovedCallback = arg =>
                {
                    using ((IDisposable)arg.CacheItem.Value) { }
                }
            });

            for (int i = 1; i <= 100000; i++)
            {
                writer.Write(Encoding.UTF8.GetBytes(i + ",AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\n"));
            }
            var dic = c.ToDictionary((kv) =>
            {
                return kv.Key;
            });
            using ((IDisposable)dic["writer"].Value) { }
            
            await Task.Delay(3000);

            var w  = c.Get("writer");

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken);
                throw new Exception("test");
            }
            _logger.LogInformation("Worker stopping at: {time}", DateTimeOffset.Now);
        }

        public override void Dispose()
        {
            using (_writer) { }
            base.Dispose();
        }
    }
}