using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DriveApp.Dash.PFC;

public class PFCOption
{
    public const string Section = "PFC";

    public PortOptions PFCPort { get; set; } = new PortOptions();
    public PortOptions CommanderPort { get; set; } = new PortOptions();
    public int InterruptWaitPollingMs { get; set; } = 200;
}

public class PortOptions
{
    public string? Name { get; set; }
    public int ReadTimeout { get; set; }
    public int WriteTimeout { get; set; }
    public int PFCInterval { get; set; }
}