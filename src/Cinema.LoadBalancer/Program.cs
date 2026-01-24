using Cinema.LoadBalancer;
using Consul;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Yarp.ReverseProxy.Configuration;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

// ------ Serilog ------
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Seq(builder.Configuration["Seq:ServerUrl"] ?? "http://seq:5341")
    .CreateLogger();
builder.Host.UseSerilog();

// Ensure high performance for Proxy
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80, o => o.Protocols = HttpProtocols.Http1AndHttp2);
});

var consulHost = builder.Configuration["Consul:Host"] ?? "consul-server"; // Default to K8s service name if exists
var consulPort = int.Parse(builder.Configuration["Consul:Port"] ?? "8500");

// Registration of Consul Client
builder.Services.AddSingleton<IConsulClient>(_ => new ConsulClient(config =>
{
    config.Address = new Uri($"http://{consulHost}:{consulPort}");
}));

// YARP Config Provider that polls Consul
builder.Services.AddSingleton<IProxyConfigProvider, ConsulProxyConfigProvider>();

builder.Services.AddReverseProxy();

builder.Services.AddHealthChecks();

// -------  OpenTelemetry ------
var serviceName = "Cinema.LoadBalancer";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: serviceName, serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://jaeger:4317");
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddPrometheusExporter());

var app = builder.Build();

app.UseSerilogRequestLogging();

app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint("/metrics");
app.MapReverseProxy();

app.Run();
