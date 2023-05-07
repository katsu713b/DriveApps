using Microsoft.Extensions.Options;
using PFC;
using System.Runtime.Caching;
using System.Text;

namespace DriveApp.Dash.PFC;

public enum OperationType: byte
{
    FromPFC = 0,
    ToPFC,
    //FromCOMM,
    //ToCOMM,

}

public class PFCLogWriter : IDisposable
{
    private readonly WriterOptions _writerOptions;
    private Lazy<FileStream> _operationLog = new Lazy<FileStream>(() =>
    {
        var dateStr = DateTime.Now.ToString("yyyy-MM-dd");
        var file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @$"logs\pfclog_operation_{dateStr}.log");
        return new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.Read);
    }, true);

    public PFCLogWriter(IOptionsMonitor<WriterOptions> options)
    {
        _writerOptions = options.CurrentValue;
    }

    public void WriteLogger(AdvancedData data)
    {
        var sm = GetOrCreateFileStream(DateTimeOffset.Now.ToUnixTimeMilliseconds());

        sm.Write(data.ToBytes());
    }
    
    public void WriteOperationLog(ReadOnlySpan<byte> data, OperationType ope)
    {
        if (data[0] == AdvancedData.Command[0]) return;
        if (data[0] == BasicData.Command[0]) return;
        if (data[0] == SensorData.Command[0]) return;
        var operation = ope.ToString();
        var txt = Encoding.UTF8.GetBytes($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] {operation}:{BitConverter.ToString(data.ToArray())}\n");
        _operationLog.Value.Write(txt);
        _operationLog.Value.Flush(true);
    }

    private FileStream GetOrCreateFileStream(long unixTimeMs)
    {
        var dto = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMs);
        var dateStr = dto.ToLocalTime().ToString("yyyy-MM-dd-HH");

        MemoryCache cache = MemoryCache.Default;
        var cacheSm = cache.Get(dateStr);
        if (cacheSm != null)
        {
            return (FileStream)cacheSm;
        }

        var file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @$"logs\pfclog_advanced_{dateStr}.dat");
        var exists = File.Exists(file);
        var s = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.Read);
        if (!exists)
        {
            // header情報書き込み
            var header = PFCLogData.CreateHeader();
            s.Write(header.AsSpan());
        }

        cache.Add(dateStr, s, new CacheItemPolicy()
        {
            SlidingExpiration = new TimeSpan(0, 0, 5),
            RemovedCallback = arg =>
            {
                using ((IDisposable)arg.CacheItem.Value) { }
            }
        });
        return s;
    }

    public void Dispose()
    {
        var dic = MemoryCache.Default.ToDictionary(kv => kv.Key, kv => (IDisposable)kv.Value);
        foreach (var sm in dic.Values)
        {
            using (sm) { }
        }

        using (_operationLog.Value) { }
    }
}

public class WriterOptions
{
    public const string Section = "WriterOption";

    public string? Name { get; set; }
    public int ReadTimeout { get; set; }
    public int WriteTimeout { get; set; }
}
