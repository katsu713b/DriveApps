using DriveApp.Dash;
using DriveApp.Dash.PFC;
using DriveApp.Dash.UI;
using System;
using System.IO;
using Wpf.Extensions.Hosting;

if (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == null)
{
    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
}


// Create a builder by specifying the application and main window.
//var builder = WpfApplication<App, MainWindow>.CreateBuilder(args);
var builder = WpfApplication<App, DashWindow>.CreateBuilder(args);



builder.Configuration.AddJsonFile(path: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "locations.json"));
builder.Configuration.AddJsonFile(path: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dashsettings.json"));

builder.Host
    .ConfigureAppConfiguration((hostingContext, config) =>
    {
        var env = hostingContext.HostingEnvironment.EnvironmentName;

        config.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
        //config.AddJsonFile("appsettings.json");
        //config.AddJsonFile($"appsettings.{env}.json", optional: true);

        //config.AddEnvironmentVariables();
        //config.AddCommandLine(args);

        config.AddJsonFile(path: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "locations.json"));
        config.AddJsonFile(path: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dashsettings.json"));
    })
    .ConfigureServices((context, services) =>
    {
        //services.AddSingleton<DashWindow>();
        services.AddSingleton<PFCLogWriter>();
        services.AddSingleton<PFCContext>();
        services.AddHostedService<PFCProvider>();
        //services.AddHostedService<PFCProxy>();

        // コンフィグを登録
        services.Configure<PFCOption>(context.Configuration.GetSection(PFCOption.Section));
        services.Configure<WriterOptions>(context.Configuration.GetSection(WriterOptions.Section));
        services.Configure<DashSettings>(context.Configuration);
    });

// Build and run the application.
var app = builder.Build();
app.RunAsync();
