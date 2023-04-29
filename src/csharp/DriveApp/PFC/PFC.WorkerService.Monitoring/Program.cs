using PFC.WorkerService.Monitoring;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostingContext, services) =>
    {
        // �R���t�B�O��o�^
        services.Configure<PortOptions>(hostingContext.Configuration.GetSection(PortOptions.Section));
        services.Configure<WriterOptions>(hostingContext.Configuration.GetSection(WriterOptions.Section));
        services.AddHostedService<Worker>();
        services.AddSingleton<Writer>();
    })
    .ConfigureLogging((hostingContext, builder) =>
    {
        //// ���O�v���o�C�_���N���A
        //builder.ClearProviders();
        //// �R���\�[�����O��ǉ�
        //builder.AddConsole();
        //// �f�o�b�O���O��ǉ�
        //builder.AddDebug();
        //// �C�x���g���O��ǉ�
        //builder.AddEventLog();
        //// �C�x���g�\�[�X��ǉ�
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
