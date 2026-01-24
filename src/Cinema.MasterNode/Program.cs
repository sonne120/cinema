using Cinema.MasterNode.Services;
using Cinema.MasterNode.Persistence;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Seq(builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341")
    .Enrich.WithProperty("Service", "Cinema.MasterNode")
    .CreateLogger();
builder.Host.UseSerilog();

// OpenTelemetry
var serviceName = "Cinema.MasterNode";
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddSource(serviceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation(options => options.SetDbStatementForText = true)
        .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint)))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter()
        .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint)));

builder.Services.AddDbContextPool<MasterDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MasterDb"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            sqlOptions.CommandTimeout(60);
            sqlOptions.MaxBatchSize(100);
        }), poolSize: 128);

builder.Services.AddSingleton(sp =>
{
    var config = builder.Configuration;
    var producerConfig = new ProducerConfig
    {
        BootstrapServers = config["Kafka:BootstrapServers"],
        Acks = Acks.All,
        EnableIdempotence = true,
        MaxInFlight = 5,
        CompressionType = CompressionType.Snappy,
        LingerMs = 10,
        BatchSize = 1048576,
        MessageSendMaxRetries = 3,
        RetryBackoffMs = 100
    };

    return new ProducerBuilder<string, string>(producerConfig)
        .SetErrorHandler((_, e) =>
            Console.WriteLine($"Kafka Error: {e.Reason}"))
        .Build();
});


builder.Services.AddSingleton<IOutboxProcessor, OutboxProcessor>();
builder.Services.AddHostedService<MasterNodeWorker>();

var app = builder.Build();

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.MapGet("/", () => "Cinema Master Node Running (Outbox Pattern)");

/* 
   Database migration/creation should be handled by an init container or the primary API service,
   not by a background worker node to avoid race conditions.
*/
// using (var scope = app.Services.CreateScope())
// {
//     var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
//     db.Database.EnsureCreated();
// }

app.Run();
