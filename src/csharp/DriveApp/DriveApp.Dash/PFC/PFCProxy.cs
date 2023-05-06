using Microsoft.Extensions.Options;
using PFC;
using System.IO.Ports;

namespace DriveApp.Dash.PFC;

public class PFCProxy : BackgroundService
{
    private readonly PFCOption _pFCOptions;
    private readonly PFCContext _pFCContext;
    private SerialPort _serialPort;
    private readonly PFCLogWriter _writer;

    public PFCProxy(IOptionsMonitor<PFCOption> options, PFCContext context, PFCLogWriter writer)
    {
        _pFCOptions = options.CurrentValue;
        _pFCContext = context;
        _writer = writer;
        _serialPort = new SerialPort();
        _pFCContext.OnApplicationShutDown += _pFCContext_OnApplicationShutDown;
    }

    private void _pFCContext_OnApplicationShutDown()
    {
        if (_serialPort.IsOpen)
            _serialPort.Close();

        using (_serialPort) { }
        using (_writer) { }
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Commander接続待ち
                await ConnectCommander();

                await WaitForPFCConnected();
                if (ct.IsCancellationRequested) return;

                _serialPort.DiscardOutBuffer();
                _serialPort.DiscardInBuffer();

                await PollingCommanderPort(ct);
            }
            catch(Exception ex) 
            {
                Console.WriteLine(ex);
                // port error
                _pFCContext.IsCommanderConnected = false;
            }
            await Task.Delay(1000);
        }
        async Task WaitForPFCConnected()
        {
            while (!ct.IsCancellationRequested && !_pFCContext.IsPFCConnected)
            {
                // PFC接続待ち
                await Task.Delay(500, ct);
            }
        }

        async Task ConnectCommander()
        {
            while (!ct.IsCancellationRequested)
            {
                // TODO: message
                if (string.IsNullOrEmpty(_pFCOptions.CommanderPort.Name))
                {
                    await Task.Delay(1000);
                    continue;
                }

                // TODO: message
                if (!SerialPort.GetPortNames().Contains(_pFCOptions.CommanderPort.Name))
                {
                    await Task.Delay(1000);
                    continue;
                }
                break;
            }

            _serialPort.SetUpPFC(_pFCOptions.CommanderPort.Name, _pFCOptions.CommanderPort.ReadTimeout, _pFCOptions.CommanderPort.WriteTimeout);

            if (!_serialPort.IsOpen)
                _serialPort.Open();
        }
    }

    private async Task PollingCommanderPort(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _serialPort.IsOpen)
        {
            var cmd = _serialPort.Read();
            if (cmd.Length == 0) continue;

            _pFCContext.IsCommanderConnected = true;

            if (cmd[0] == AdvancedData.Command[0] && _pFCContext.LatestAdvancedData != null)
            {
                var data = _pFCContext.LatestAdvancedData.RawData;
                WriteCmd(data);
                continue;
            }
            else if (cmd[0] == BasicData.Command[0] && _pFCContext.LatestAdvancedData != null)
            {
                var data = _pFCContext.LatestAdvancedData.ConvertToBasicRaw();
                WriteCmd(data);
                continue;
            }

            var res = await _pFCContext.GetData(cmd);
            if (res.Length == 0) continue;

            WriteCmd(res);
        }

        void WriteCmd(byte[] cmd)
        {
            try
            {
                _serialPort.DiscardInBuffer();
                _serialPort.DiscardOutBuffer();
                _serialPort.Write(cmd, 0, cmd.Length);
            }
            catch
            {
            }
        }
    }


    //private void PollingCommanderPort(CancellationToken ct)
    //{
    //    const int iLen = 1;

    //    byte[] buffer = new byte[256];

    //    while (!ct.IsCancellationRequested && _serialPort.IsOpen)
    //    {
    //        // Soft/Commander -> PFC
    //        //_serialPort.Read(buffer, 0, 2);
    //        //_serialPort.Read(buffer, 2, buffer[iLen] - 1);
    //        _serialPort.Read(ref buffer);

    //        _pFCContext.IsCommanderConnected = true;

    //        ReadOnlySpan<byte> span = buffer;
    //        var cmd = span[..(span[iLen] + 1)];

    //        if (cmd[0] == AdvancedData.Command[0] && _pFCContext.LatestAdvancedData != null)
    //        {
    //            var data = _pFCContext.LatestAdvancedData.RawData;
    //            _serialPort.Write(data, 0, data.Length);
    //            continue;
    //        }
    //        else if (cmd[0] == BasicData.Command[0] && _pFCContext.LatestAdvancedData != null)
    //        {
    //            var data = _pFCContext.LatestAdvancedData.ConvertToBasicRaw();
    //            _serialPort.Write(data, 0, data.Length);
    //            continue;
    //        }

    //        _writer.WriteOperationLog(cmd, OperationType.ToPFC);

    //        var res = await _pFCContext.GetData(cmd.ToArray());
    //        _writer.WriteOperationLog(res, OperationType.FromPFC);

    //        _serialPort.Write(res, 0, res.Length);
    //    }
    //}

    private async Task<string> WaitFoundPortAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            if (string.IsNullOrEmpty(_pFCOptions.CommanderPort.Name))
            {
                await Task.Delay(2000, ct);
                continue;
            }
            if (!SerialPort.GetPortNames().Any(name => name == _pFCOptions.CommanderPort.Name))
            {
                await Task.Delay(2000, ct);
                continue;
            }
            return _pFCOptions.CommanderPort.Name;
        }

        return string.Empty;
    }

    public override void Dispose()
    {
        using (_serialPort) { }
        base.Dispose();
    }
}
