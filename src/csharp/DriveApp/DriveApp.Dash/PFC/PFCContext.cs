using Microsoft.Extensions.Options;
using PFC;
using System.Collections.Concurrent;

namespace DriveApp.Dash.PFC;

public class PFCContext: IDisposable
{
    public delegate Task<byte[]> InterruptWriteHandler(byte[] data);
    public event InterruptWriteHandler? OnInterruptWrite = null;

    public delegate void ApplicationShutDownHandler();
    public event ApplicationShutDownHandler? OnApplicationShutDown = null;

    private PFCOption _option;

    public PFCContext(IOptionsMonitor<PFCOption> options)
    {
        _option = options.CurrentValue;
    }

    public Task<byte[]> GetData(byte[] command)
    {
        if (OnInterruptWrite == null) throw new InvalidOperationException(nameof(OnInterruptWrite));

        StartPolling = false;
        _interruptWait.Enqueue(true);
        WaitPolling();

        return OnInterruptWrite(command);
    }

    private async Task WaitPolling()
    {
        await Task.Delay(_option.InterruptWaitPollingMs);
        _interruptWait.TryDequeue(out _);
        if (_interruptWait.Count == 0)
            StartPolling = true;
    }

    public void Dispose()
    {
        if (OnApplicationShutDown != null)
            OnApplicationShutDown();
    }

    public AdvancedData? LatestAdvancedData { get; set; } = null;

    public bool IsPFCConnected { get; set; }
    public bool IsCommanderConnected { get; set; }

    public bool StartPolling { get; set; }

    private readonly Dictionary<string, byte[]> _commanderInfo = new Dictionary<string, byte[]>();
    public Dictionary<string, byte[]> CommanderInfo => _commanderInfo;

    private readonly ConcurrentQueue<bool> _interruptWait = new ConcurrentQueue<bool>();
}
