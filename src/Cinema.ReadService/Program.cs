using Cinema.ReadService;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

builder.Services.AddReadService(builder.Configuration);

var host = builder.Build();
host.Run();
