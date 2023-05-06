using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using System.Runtime.Intrinsics.Arm;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PFC;

public abstract class ResponseBase
{
    protected readonly long _unixTimeMs;
    protected readonly byte[] _rawData;

    public ResponseBase(long unixTimeMs, byte[] rawData)
    {
        _unixTimeMs = unixTimeMs;
        _rawData = rawData;
    }
    public ResponseBase(Span<byte> data)
    {
        _unixTimeMs = BitConverter.ToInt64(data.Slice(0, 8));
        _rawData = data.Slice(8).ToArray();
    }

    public byte[] ToBytes()
    {
        Span<byte> data = stackalloc byte[8 + _rawData.Length];
        BitConverter.GetBytes(_unixTimeMs).CopyTo(data);
        _rawData.CopyTo(data.Slice(8));

        return data.ToArray();
    }
    public static bool ChecksumVerification(ReadOnlySpan<byte> data)
    {
        byte buf = 0;
        for (int i = 0; i < data.Length; i++)
        {
            buf = (byte)(data[i] + buf);
        }

        return buf == 255;
    }

    public static byte CalcChecksum(ReadOnlySpan<byte> data)
    {
        byte buf = 0;
        for (int i = 0; i < data.Length; i++)
        {
            buf = (byte)(data[i] + buf);
        }

        return (byte)(~buf);
    }

    public abstract bool IsValid { get; }

    //protected ReadOnlySpan<byte> AsSpan(int start, int length) => _rawData.AsSpan().Slice(start, length);

    public long ReceivedUnixTimeMs => _unixTimeMs;
    public byte[] RawData => _rawData;

    /// <summary>
    /// Engine Speed(RPM)
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    protected static ushort ParseRpm(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source);
    /// <summary>
    /// Vehicle Speed(Km/h)
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    protected static ushort ParseSpeed(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source);
    /// <summary>
    /// Boost(Kg/cm2)
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    protected static float ParseAirIPressure(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source) * 0.0001f;
    /// <summary>
    /// Sensor Volt(mV)
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    protected static ushort ParseSensorVoltage(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source);
    /// <summary>
    /// INJ width(ms)
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    protected static float ParseInjectorWidth(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source) / 256f;
    /// <summary>
    /// fuel correction	
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    protected static float ParseFuelCorrection(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source) / 256f;
    /// <summary>
    /// Ignition Angle(deg)
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    protected static int ParseIGNAngle(ReadOnlySpan<byte> source) => source[0] - 25;
    /// <summary>
    /// Temp(deg.C)
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    protected static int ParseTmep(ReadOnlySpan<byte> source) => source[0] - 80;
    /// <summary>
    /// Metaling Oil PumpDuty(%)
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    protected static float ParseMeteringOilPumpPosition(ReadOnlySpan<byte> source) => source[0] * 0.828125f;// (212f / 256f)
    protected static float ParseBoostDuty(ReadOnlySpan<byte> source) => source[0] * 0.4f;
    protected static ushort ParseKnockLevel(ReadOnlySpan<byte> source) => source[0];
    /// <summary>
    /// Battery Voltage(V)
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    protected static float ParseBattVoltage(ReadOnlySpan<byte> source) => source[0] * 0.1f;
    protected static float ParseISCVDuty(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source) * 0.1f;
    protected static float ParseO2Voltage(ReadOnlySpan<byte> source) => source[0] * 0.02f;
    protected static float ParseInjectorDuty(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source) * 0.1f;
}

public sealed class BasicData : ResponseBase
{
    public BasicData(long unixTimeMs, byte[] basicData) : base(unixTimeMs, basicData) { }
    public BasicData(byte[] basicData) : base(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), basicData) { }

    public static readonly byte[] Command = new byte[] { 0xDA, 0x02, 0x23 };

    // TODO
    public override bool IsValid => true;

    public float InjectorDuty => ParseInjectorDuty(_rawData[2..4]);
    public int IGNAngleLd => ParseIGNAngle(_rawData[4..5]);
    public int IGNAngleTr => ParseIGNAngle(_rawData[6..7]);
    /// <summary>
    /// Engine Speed(RPM)
    /// </summary>
    //public int Rpm => ParseRpm(AsSpan(8, 2));
    public int Rpm => ParseRpm(_rawData[8..10]);
    /// <summary>
    /// Vehicle Speed(Km/h)
    /// </summary>
    public int Speed => ParseSpeed(_rawData[10..12]);
    /// <summary>
    /// Boost(Kg/cm2)
    /// </summary>
    public float AirIPressure => ParseAirIPressure(_rawData[12..14]);
    /// <summary>
    /// Knocking Level
    /// </summary>
    public int KnockLevel => ParseKnockLevel(_rawData[4..5]);
    /// <summary>
    /// (deg.C)
    /// </summary>
    public int WaterTemp => ParseTmep(_rawData[16..17]);
    /// <summary>
    /// (deg.C)
    /// </summary>
    public int AirTemp => ParseTmep(_rawData[18..19]);
    /// <summary>
    /// Battery Voltage(V)
    /// </summary>
    public float BattVoltage => ParseBattVoltage(_rawData[20..21]);
}

public sealed class AdvancedData : ResponseBase
{
    public byte[] ConvertToBasicRaw()
    {
        var data = new byte[23];
        data[0] = BasicData.Command[0];
        data[1] = 22;
        // Inj Duty
        var injduty = (int)(GetInjectorPrDuty() * 10);

        data[2] = (byte)(injduty % 256); //  0.1%
        data[3] = (byte)(injduty / 256); // 25.6%
        // IGN Ld
        data[4] = _rawData[14];
        // IGN Tr
        data[6] = _rawData[15];
        // RPM
        data[8] = _rawData[2];
        data[9] = _rawData[3];
        // speed
        data[10] = _rawData[24];
        data[11] = _rawData[25];
        //AirIPressure
        // kg/cm2 -> mmHg
        // 1 mmHg = 0.00131579 kg/cm2
        // 0 = -760
        var boostmmhg = AirIPressure / 0.00131579;

        data[12] = (byte)(boostmmhg % 256);
        data[13] = (byte)(boostmmhg / 256);
        // Knock
        data[14] = _rawData[22];
        //data[15] = 0;
        // WAT
        data[16] = _rawData[20];
        // Air
        data[18] = _rawData[21];
        // BAT
        data[20] = _rawData[23];
        data[21] = 0;

        //data[17] = 48;
        //data[19] = 47;

        // Checksum
        data[22] = CalcChecksum(data[0..23]);

        return data;
    }

    public AdvancedData(long unixTimeMs, byte[] rawData) : base(unixTimeMs, rawData)
    {
        if (rawData.Length != RawDataLength) return;
        if (rawData[0] != ResponseHeader[0]) return;
        if (rawData[1] != ResponseHeader[1]) return;
        if (!ChecksumVerification(rawData)) return;
        _isValid = true;
    }
    //public AdvancedData(byte[] advancedData) : base(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), advancedData) { }

    public static readonly byte[] Command = new byte[] { 0xF0, 0x02, 0x0D };
    public static readonly byte[] ResponseHeader = new byte[] { 0xF0, 0x20 };

    private bool _isValid = false;
    public override bool IsValid => _isValid;

    /// <summary>
    /// ログデータサイズ
    /// 受信時刻(UnixTimeMs) 8byte + RawData 33byte
    /// </summary>
    public const int DataLength = 41;
    /// <summary>
    /// PFC本体から受信するデータサイズ
    /// </summary>
    public const int RawDataLength = 33;
    //private static readonly MemoryMappedFile _shareMemory = MemoryMappedFile.CreateOrOpen("PFC_MEMORY_ADVANCED_DATA_LATEST", DataLength);
    //private static readonly MemoryMappedViewStream _accessor = _shareMemory.CreateViewStream();

    //private static void UpdateShareMemory(byte[] data)
    //{
    //    _accessor.Write(data, 0, data.Length);
    //}

    public float GetInjectorPrDuty()
    {
        return InjectorWidthPrimary / (600.0f / Rpm);
    }

    /// <summary>
    /// Engine Speed(RPM)
    /// </summary>
    public int Rpm => ParseRpm(_rawData[2..4]);
    /// <summary>
    /// Boost(Kg/cm2)
    /// </summary>
    public float AirIPressure => ParseAirIPressure(_rawData[4..6]);
    /// <summary>
    /// Sensor Volt(mV)
    /// </summary>
    public int MapSensorVoltage => ParseSensorVoltage(_rawData[6..8]);
    /// <summary>
    /// Sensor Volt(mV)
    /// </summary>
    public int ThrottleSensorVoltage => ParseSensorVoltage(_rawData[8..10]);
    /// <summary>
    /// INJ width(ms)
    /// </summary>
    public float InjectorWidthPrimary => ParseInjectorWidth(_rawData[10..12]);
    /// <summary>
    /// 
    /// </summary>
    public float FuelCorrection => ParseFuelCorrection(_rawData[12..14]);
    /// <summary>
    /// Leading Ignition Angle(deg)
    /// </summary>
    public int IGNAngleLd => ParseIGNAngle(_rawData[14..15]);
    /// <summary>
    /// Trailing Ignition Angle(deg)
    /// </summary>
    public int IGNAngleTr => ParseIGNAngle(_rawData[15..16]);
    /// <summary>
    /// (deg.C)
    /// </summary>
    public int FuelTemp => ParseTmep(_rawData[16..17]);
    /// <summary>
    /// Metaling Oil PumpDuty(%)
    /// </summary>
    public float MOPPosition => ParseMeteringOilPumpPosition(_rawData[17..18]);
    /// <summary>
    /// (%)
    /// </summary>
    public float BoostDutyTP => ParseBoostDuty(_rawData[18..19]);
    /// <summary>
    /// (%)
    /// </summary>
    public float BoostDutyWG => ParseBoostDuty(_rawData[19..20]);
    /// <summary>
    /// (deg.C)
    /// </summary>
    public int WaterTemp => ParseTmep(_rawData[20..21]);
    /// <summary>
    /// (deg.C)
    /// </summary>
    public int AirTemp => ParseTmep(_rawData[21..22]);
    /// <summary>
    /// Knocking Level
    /// </summary>
    public int KnockLevel => ParseKnockLevel(_rawData[22..23]);
    /// <summary>
    /// Battery Voltage(V)
    /// </summary>
    public float BattVoltage => ParseBattVoltage(_rawData[23..24]);
    /// <summary>
    /// Vehicle Speed(Km/h)
    /// </summary>
    public int Speed => ParseSpeed(_rawData[24..26]);
    /// <summary>
    /// (%)
    /// </summary>
    public float ISCVDuty => ParseISCVDuty(_rawData[26..28]);
    public float O2Voltage => ParseO2Voltage(_rawData[28..29]);
    /// <summary>
    /// INJ width(ms)
    /// </summary>
    public float InjectorWidthSecondary => ParseInjectorWidth(_rawData[30..32]);
    public ReadOnlySpan<byte> CheckSum => _rawData[33..];
}

public sealed class SensorData: ResponseBase
{
    public SensorData(long unixTimeMs, byte[] basicData) : base(unixTimeMs, basicData) { }

    public static readonly byte[] Command = new byte[] { 0xDE, 0x02, 0x1F };
    // TODO
    public override bool IsValid => true;
}
