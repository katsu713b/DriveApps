namespace DriveApp.Dash;

public class DashSettings
{
    public const string Section = "DashSettings";

    public double RpmMaxValue { get; set; } = 8000;
    public int ThrottleVoltageMinValue { get; set; } = 510;
    public int ThrottleVoltageMaxValue { get; set; } = 4400;
    public Warnings WarningsValue { get; set; } = new Warnings();

    public class Warnings
    {
        public int CautionWaterTemp { get; set; } = 95;
        public int CautionAirTemp { get; set; } = 70;
        public int CautionFuelTemp { get; set; } = 50;
        public int CautionKnock { get; set; } = 50;
        public int CautionRpm { get; set; } = 7250;
        public int WarnRpm { get; set; } = 7400;
    }
}
