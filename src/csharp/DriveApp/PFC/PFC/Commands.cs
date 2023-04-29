using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PFC;

//public static class Commands
//{
//    public static readonly byte[] ADVANCED = new byte[] { 0xF0, 0x02, 0x0D };
//    public static readonly byte[] BASIC = new byte[] { 0xDA, 0x02, 0x23 };
//    public static readonly byte[] SENSOR = new byte[] { 0xDD, 0x02, 0x20 };
//    public static readonly byte[] MAP_INDICES = new byte[] { 0xDB, 0x02, 0x22 };
//}

/*
 * init F3 02 0A
 */


public enum Commands : byte
{
    ADVANCED = 1,
    BASIC,
    SENSOR,
    MAP_INDICES
}
