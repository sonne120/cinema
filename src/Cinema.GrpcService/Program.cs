using Cinema.Application;
using Cinema.Infrastructure;
using Cinema.GrpcService.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Cinema.ServiceDiscovery;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5001, o => o.Protocols = HttpProtocols.Http2);
});

builder.Services.AddGrpc();

// Traces are pushed over OTLP, so the h2c-only listener is not a problem here —
// unlike Prometheus scraping, which cannot reach an HTTP/2-only endpoint.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "Cinema.GrpcService", serviceVersion: "1.0.0")
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName
        }))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://jaeger:4317");
        }));
builder.Services.AddHealthChecks();

// Kestrel here is HTTP/2-only, which Consul's HTTP check (HTTP/1.1) cannot probe,
// so fall back to a TCP connect check.
builder.Services.AddConsulServiceRegistration(
    builder.Configuration,
    serviceName: "cinema-grpc",
    serviceHost: builder.Configuration["Consul:ServiceHost"] ?? "localhost",
    servicePort: int.Parse(builder.Configuration["Consul:ServicePort"] ?? "5001"),
    tags: new[] { "grpc", "cinema", "dotnet", "read" },
    checkType: ConsulCheckType.Tcp);

builder.Services.AddApplication();

builder.Services.AddInfrastructure(builder.Configuration, isWriteSide: false);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

app.MapGrpcService<CinemaGrpcService>();
app.MapHealthChecks("/health");

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
