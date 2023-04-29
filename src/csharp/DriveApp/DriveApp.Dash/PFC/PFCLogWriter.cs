using Microsoft.Extensions.Options;
using PFC;
using System.IO;
using System.Runtime.Caching;

namespace DriveApp.Dash.PFC;

public class PFCLogWriter : IDisposable
{
    private readonly WriterOptions _writerOptions;

    public PFCLogWriter(IOptionsMonitor<WriterOptions> options)
    {
        _writerOptions = options.CurrentValue;
    }

    public void WriteToFile(AdvancedData data)
    {
        var sm = GetOrCreateFileStream(DateTimeOffset.Now.ToUnixTimeMilliseconds());

        var dataBytes = data.ToBytes();
        sm.Write(dataBytes);
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
    }
}

public class WriterOptions
{
    public const string Section = "WriterOption";

    public string? Name { get; set; }
    public int ReadTimeout { get; set; }
    public int WriteTimeout { get; set; }
}
