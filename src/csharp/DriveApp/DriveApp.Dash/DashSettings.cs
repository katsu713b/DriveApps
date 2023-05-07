namespace DriveApp.Dash;

public class DashSettings
{
    public const string Section = "DashSettings";

    public double RpmMaxValue { get; set; }
    public int ThrottleVoltageMinValue { get; set; }
    public int ThrottleVoltageMaxValue { get; set; }
    public DashSettingWarnings Warnings { get; set; }

}

public class DashSettingWarnings
{
    public int CautionWaterTemp { get; set; } = 95;
    public int CautionAirTemp { get; set; } = 70;
    public int CautionFuelTemp { get; set; } = 50;
    public int CautionKnock { get; set; } = 50;
    public int CautionRpm { get; set; }
    public int WarnRpm { get; set; }
}