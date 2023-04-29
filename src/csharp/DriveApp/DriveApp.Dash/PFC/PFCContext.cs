using PFC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DriveApp.Dash.PFC
{
    public class PFCContext
    {
        public delegate byte[] InterruptWriteHandler(byte[] data);
        public event InterruptWriteHandler OnInterruptWrite;

        public PFCContext() { }

        public byte[] GetData(byte[] command)
        {
            if (OnInterruptWrite == null) throw new InvalidOperationException(nameof(OnInterruptWrite));

            return OnInterruptWrite(command);
        }

        public AdvancedData LatestAdvancedData { get; set; }
    }
}
