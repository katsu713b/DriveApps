using Microsoft.Extensions.Options;
using PFC;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DriveApp.Dash.PFC;

public class PFCProxy : BackgroundService
{
    private readonly PFCOption _pFCOptions;
    private readonly PFCContext _pFCContext;
    private SerialPort _serialPort;

    public PFCProxy(IOptionsMonitor<PFCOption> options, PFCContext context)
    {
        _pFCOptions = options.CurrentValue;
        _pFCContext = context;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await WaitFoundPortAsync(ct);
        
        if (ct.IsCancellationRequested)
            return;

        _serialPort = new SerialPort(_pFCOptions.CommanderPort.Name, 19200, Parity.Even, 8, StopBits.One);

        //_serialPort.ReadTimeout = _pFCOptions.CommanderPort.ReadTimeout;
        //_serialPort.WriteTimeout = _pFCOptions.CommanderPort.WriteTimeout;
        
        await Task.Delay(1200);

        _serialPort.Open();
        _serialPort.DiscardOutBuffer();
        _serialPort.DiscardInBuffer();
        

        PollingCommanderPort(ct);
    }

    private void PollingCommanderPort(CancellationToken ct)
    {
        const int iLen = 1;

        byte[] buffer = new byte[256];

        while (!ct.IsCancellationRequested)
        {
            // Soft/Commander -> PFC
            _serialPort.Read(buffer, 0, 2);
            _serialPort.Read(buffer, 2, buffer[iLen] - 1);

            Span<byte> span = buffer;
            var cmd = span[..(span[iLen] + 1)];
            if (cmd.SequenceEqual(AdvancedData.Command) && _pFCContext.LatestAdvancedData != null)
            {
                var data = _pFCContext.LatestAdvancedData.RawData;
                _serialPort.Write(data, 0, data.Length);
                continue;
            }
            
            var res = _pFCContext.GetData(cmd.ToArray());

            _serialPort.Write(res, 0, res.Length);
        }
    }

    private async Task WaitFoundPortAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            if (SerialPort.GetPortNames().Any(name => name == _pFCOptions.CommanderPort.Name))
            {
                break;
            }
            await Task.Delay(2000, ct);
        }
    }
}
