using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PFCComm
{
    internal static class Helper
    {
        public static byte[] ConvertToByte(ReadOnlySpan<char> source)
        {
            byte[] byteArray = new byte[source.Length / 2];  // byte配列の初期化

            for (int i = 0; i < source.Length; i += 2)
            {
                byteArray[i / 2] = Convert.ToByte(new String(source.Slice(i, 2)), 16);  // 16進数の文字列をbyteに変換
            }
            return byteArray;
        }
    }

    internal abstract class ResponseBase
    {
        protected readonly byte[] _data;
        
        public ResponseBase(byte[] advancedData)
        {
            _data = advancedData;
        }
        
        protected ReadOnlySpan<byte> AsSpan() => _data.AsSpan();
        protected ReadOnlySpan<byte> AsSpan(int start, int length) => _data.AsSpan().Slice(start, length);

        /// <summary>
        /// Engine Speed(RPM)
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        protected ushort ParseRpm(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source);
        /// <summary>
        /// Vehicle Speed(Km/h)
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        protected ushort ParseSpeed(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source);
        /// <summary>
        /// Boost(Kg/cm2)
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        protected float ParseBoost(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source) * 0.0001f;
        /// <summary>
        /// Sensor Volt(mV)
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        protected ushort ParseSensorVoltage(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source);
        /// <summary>
        /// INJ width(ms)
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        protected float ParseInjectorWidth(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source) / 256f;
        protected float ParseFuelCorrection(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source) / 256f;
        /// <summary>
        /// Ignition Angle(deg)
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        protected int ParseIGNAngle(ReadOnlySpan<byte> source) => source[0] - 25;
        /// <summary>
        /// Temp(deg.C)
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        protected int ParseTmep(ReadOnlySpan<byte> source) => source[0] - 80;
        /// <summary>
        /// Metaling Oil PumpDuty(%)
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        protected float ParseMeteringOilPumpPosition(ReadOnlySpan<byte> source) => source[0] * 0.828125f;// (212f / 256f)
        protected float ParseBoostDuty(ReadOnlySpan<byte> source) => source[0] * 0.4f;
        protected ushort ParseKnockLevel(ReadOnlySpan<byte> source) => source[0];
        /// <summary>
        /// Battery Voltage(V)
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        protected float ParseBattVoltage(ReadOnlySpan<byte> source) => source [0] * 0.1f;
        protected float ParseISCVDuty(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source) * 0.1f;
        protected float ParseO2Voltage(ReadOnlySpan<byte> source) => source[0] * 0.02f;
        protected float ParseInjectorDuty(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source) * 0.1f;
    }

    internal sealed class BasicData : ResponseBase
    {
        public BasicData(byte[] advancedData) : base(advancedData) { }

        public float InjectorDuty => ParseInjectorDuty(AsSpan(2, 2));
        public int IGNAngleLd => ParseIGNAngle(AsSpan(4, 1));
        public int IGNAngleTr => ParseIGNAngle(AsSpan(6, 1));
        /// <summary>
        /// Engine Speed(RPM)
        /// </summary>
        public int Rpm => ParseRpm(AsSpan(8, 2));
        /// <summary>
        /// Vehicle Speed(Km/h)
        /// </summary>
        public int Speed => ParseSpeed(AsSpan(10, 2));
        /// <summary>
        /// Boost(Kg/cm2)
        /// </summary>
        public float Boost => ParseBoost(AsSpan(12, 2));
        /// <summary>
        /// Knocking Level
        /// </summary>
        public int KnockLevel => ParseKnockLevel(AsSpan(14, 1));
        /// <summary>
        /// (deg.C)
        /// </summary>
        public int WaterTemp => ParseTmep(AsSpan(16, 1));
        /// <summary>
        /// (deg.C)
        /// </summary>
        public int AirTemp => ParseTmep(AsSpan(18, 1));
        /// <summary>
        /// Battery Voltage(V)
        /// </summary>
        public float BattVoltage => ParseBattVoltage(AsSpan(20, 1));
    }

    internal sealed class AdvancedData : ResponseBase
    {
        public AdvancedData(byte[] advancedData): base(advancedData) { }

        /// <summary>
        /// Engine Speed(RPM)
        /// </summary>
        public int Rpm => ParseRpm(AsSpan(2, 2));
        /// <summary>
        /// Boost(Kg/cm2)
        /// </summary>
        public float Boost => ParseBoost(AsSpan(4, 2));
        /// <summary>
        /// Sensor Volt(mV)
        /// </summary>
        public int MapSensorVoltage => ParseSensorVoltage(AsSpan(6, 2));
        /// <summary>
        /// Sensor Volt(mV)
        /// </summary>
        public int ThrottleSensorVoltage => ParseSensorVoltage(AsSpan(8, 2));
        /// <summary>
        /// INJ width(ms)
        /// </summary>
        public float InjectorWidthPrimary => ParseInjectorWidth(AsSpan(10, 2));
        /// <summary>
        /// 
        /// </summary>
        public float FuelCorrection => ParseFuelCorrection(AsSpan(12, 2));
        /// <summary>
        /// Leading Ignition Angle(deg)
        /// </summary>
        public int IGNAngleLd => ParseIGNAngle(AsSpan(14, 1));
        /// <summary>
        /// Trailing Ignition Angle(deg)
        /// </summary>
        public int IGNAngleTr => ParseIGNAngle(AsSpan(15, 1));
        /// <summary>
        /// (deg.C)
        /// </summary>
        public int FuelTemp => ParseTmep(AsSpan(16, 1));
        /// <summary>
        /// Metaling Oil PumpDuty(%)
        /// </summary>
        public float MeteringOilPumpPosition => ParseMeteringOilPumpPosition(AsSpan(17,1));
        /// <summary>
        /// (%)
        /// </summary>
        public float BoostDutyTP => ParseBoostDuty(AsSpan(18, 1));
        /// <summary>
        /// (%)
        /// </summary>
        public float BoostDutyWG => ParseBoostDuty(AsSpan(19, 1));
        /// <summary>
        /// (deg.C)
        /// </summary>
        public int WaterTemp => ParseTmep(AsSpan(20, 1));
        /// <summary>
        /// (deg.C)
        /// </summary>
        public int AirTemp => ParseTmep(AsSpan(21, 1));
        /// <summary>
        /// Knocking Level
        /// </summary>
        public int KnockLevel => ParseKnockLevel(AsSpan(22, 1));
        /// <summary>
        /// Battery Voltage(V)
        /// </summary>
        public float BattVoltage => ParseBattVoltage(AsSpan(23, 1));
        /// <summary>
        /// Vehicle Speed(Km/h)
        /// </summary>
        public int Speed => ParseSpeed(AsSpan(24, 2));
        /// <summary>
        /// (%)
        /// </summary>
        public float ISCVDuty => ParseISCVDuty(AsSpan(26, 2));
        public float O2Voltage => ParseO2Voltage(AsSpan(28, 1));
        /// <summary>
        /// INJ width(ms)
        /// </summary>
        public float InjectorWidthSecondary => ParseInjectorWidth(AsSpan(30, 2));
        public ReadOnlySpan<byte> CheckSum => AsSpan(33, 1);
    }
}
