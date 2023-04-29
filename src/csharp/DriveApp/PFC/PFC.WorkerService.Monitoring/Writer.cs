using Microsoft.Extensions.Options;
using System.IO.MemoryMappedFiles;
using System.Runtime.Caching;

namespace PFC.WorkerService.Monitoring;

public class Writer : IDisposable
{
    private readonly WriterOptions _writerOptions;
    private MemoryMappedViewAccessor _mmf;

    public Writer(IOptions<WriterOptions> options)
    {
        _writerOptions = options.Value;

        // Open shared memory
        _mmf = MemoryMappedFile.CreateNew("PFC_Latest_AdvancedData", AdvancedData.DataLength).CreateViewAccessor();
    }

    /*
     SharedMemory:
       latest basic data:Total 32byte
         byte size(1byte) | long readDateTime UnixTimeMilliseconds(8byte) | byte data(23byte)
       latest advanced data:Total 42byte
         byte size(1byte) | long readDateTime UnixTimeMilliseconds(8byte) | byte data(33byte)
       latest sensor string:Total 92byte
         byte size(1byte) | long readDateTime UnixTimeMilliseconds(8byte) | byte data(83byte)
       latest sensor:Total 30byte
         byte size(1byte) | long readDateTime UnixTimeMilliseconds(8byte) | byte data(max 21byte)
       max
     */
    public void WriteToMemoryAndFile(AdvancedData data)
    {
        var sm = GetOrCreateFileStream(DateTimeOffset.Now.ToUnixTimeMilliseconds());

        var dataBytes = data.ToBytes();
        sm.Write(dataBytes);

        _mmf.WriteArray(0, dataBytes, 0, dataBytes.Length);

        var txt = BitConverter.ToString(data.RawData.ToArray());
        Console.WriteLine(txt);
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
        //var s = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.Read, 1024 * 4, true);
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
        using (_mmf) { }
        
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
