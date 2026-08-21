using Cinema.ReadService;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Cinema.ServiceDiscovery;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Seq(builder.Configuration["Seq:ServerUrl"] ?? "http://seq:5341")
    .Enrich.WithProperty("Service", "Cinema.ReadService")
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

// OpenTelemetry
var serviceName = "Cinema.ReadService";
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddSource(serviceName)
        .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint)))
    .WithMetrics(metrics => metrics
        .AddRuntimeInstrumentation()
        .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint)));

builder.Services.AddReadService(builder.Configuration);

// This is a plain worker host with no HTTP listener, so Consul cannot probe it.
// It reports liveness itself via a TTL heartbeat instead.
builder.Services.AddConsulServiceRegistration(
    builder.Configuration,
    serviceName: "cinema-read-service",
    serviceHost: builder.Configuration["Consul:ServiceHost"] ?? "localhost",
    servicePort: int.Parse(builder.Configuration["Consul:ServicePort"] ?? "0"),
    tags: new[] { "read-service", "cinema", "dotnet", "worker" },
    checkType: ConsulCheckType.Ttl);

var host = builder.Build();
host.Run();
