using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace DriveApp.GPSLapTimer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static App()
        {

            if (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == null)
            {
                Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
            }

            IConfigurationRoot configRoot = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(path: $"locations.json")
                .AddJsonFile(path: $"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")}.json")
                .AddEnvironmentVariables(prefix: "DOTNET_")
                .Build();

            Config = new Config(configRoot);
        }
        internal static Config Config { get; private set; }
    }
}
