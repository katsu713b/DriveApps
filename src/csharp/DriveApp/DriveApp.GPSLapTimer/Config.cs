using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DriveApp.GPSLapTimer
{
    internal class Config
    {
        public Config(IConfigurationRoot root)
        {
            if (root == null) throw new ArgumentNullException("root"); 
            
            Gps = root.GetSection("Gps").Get<GpsOption>() ?? throw new ArgumentNullException("Gps setting");
            Locations = root.GetSection("Location").Get<CircuitLocation[]>() ?? throw new ArgumentNullException("Location setting");
        }

        public bool IsDevelop => Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development";

        public GpsOption Gps { get; }
        public CircuitLocation[] Locations { get; }

        public class GpsOption
        {
            public string Port { get; set; }
            public int Rate { get; set; }
        }

        public class CircuitLocation
        {
            public string? Name { get; set; }
            public Line? ControlLine { get; set; }
            public Line? Sector1Line { get; set; }
            public Line? Sector2Line { get; set; }
            public double[][]? AreaPoints { get; set; }

            public class Line
            {
                public double[]? Start { get; set; }
                public double[]? End { get; set; }
            }
        }
    }
}
