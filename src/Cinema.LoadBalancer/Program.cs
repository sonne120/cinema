using Cinema.LoadBalancer;
using Consul;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Yarp.ReverseProxy.Configuration;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

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

// The Magic: YARP Config Provider that polls Consul
builder.Services.AddSingleton<IProxyConfigProvider, ConsulProxyConfigProvider>();

builder.Services.AddReverseProxy();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapReverseProxy();

app.Run();
