namespace PFC;

public static class PFCLogData
{
    public const byte CurrentVer = 1;
    public static byte[] CreateHeader()
    {
        var header = new byte[32];
        header[0] = CurrentVer;              // logdata ver
        header[1] = (byte)Commands.ADVANCED; // cmd
        header[2] = AdvancedData.DataLength; // dataLnegth

        return header.ToArray();
    }
}
