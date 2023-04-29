using PFC.WorkerService.Monitoring;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostingContext, services) =>
    {
        // コンフィグを登録
        services.Configure<PortOptions>(hostingContext.Configuration.GetSection(PortOptions.Section));
        services.Configure<WriterOptions>(hostingContext.Configuration.GetSection(WriterOptions.Section));
        services.AddHostedService<Worker>();
        services.AddSingleton<Writer>();
    })
    .ConfigureLogging((hostingContext, builder) =>
    {
        //// ログプロバイダをクリア
        //builder.ClearProviders();
        //// コンソールログを追加
        //builder.AddConsole();
        //// デバッグログを追加
        //builder.AddDebug();
        //// イベントログを追加
        //builder.AddEventLog();
        //// イベントソースを追加
        //builder.AddEventSourceLogger();

        builder.AddSimpleConsole(options =>
        {
            options.IncludeScopes = true;
            options.SingleLine = true;
            options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
        });
    })
    .ConfigureAppConfiguration((hostingContext, config) =>
    {
        //var env = hostingContext.HostingEnvironment.EnvironmentName;
        //config.SetBasePath(Directory.GetCurrentDirectory());
        //config.AddJsonFile("appsettings.json");
        //config.AddJsonFile($"appsettings.{env}.json", optional: true);

        //config.AddEnvironmentVariables();
        //config.AddCommandLine(args);

    })
    .Build();

host.Run();
