using DriveApp.Dash;
using DriveApp.Dash.PFC;
using DriveApp.Dash.UI;

if (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == null)
{
    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
}

// Create a builder by specifying the application and main window.
//var builder = WpfApplication<App, MainWindow>.CreateBuilder(args);
var builder = WpfApplication<App, DashWindow>.CreateBuilder(args);

builder.Host
    .ConfigureAppConfiguration((hostingContext, config) =>
    {
        var env = hostingContext.HostingEnvironment.EnvironmentName;

        //config.AddEnvironmentVariables();
        //config.AddCommandLine(args);

        //config.AddJsonFile("locations.json");
        config.AddJsonFile("dashsettings.json");
    })
    .ConfigureServices((context, services) =>
    {
        //services.AddSingleton<DashWindow>();
        services.AddSingleton<PFCLogWriter>();
        services.AddSingleton<PFCContext>();
        services.AddHostedService<PFCProvider>();
        //services.AddHostedService<PFCProxy>();

        // ƒRƒ“ƒtƒBƒO‚ð“o˜^
        services.Configure<PFCOption>(context.Configuration.GetSection(PFCOption.Section));
        services.Configure<WriterOptions>(context.Configuration.GetSection(WriterOptions.Section));
        services.Configure<DashSettings>(context.Configuration.GetSection(DashSettings.Section));

        
    });

// Build and run the application.
var app = builder.Build();

app.RunAsync();
