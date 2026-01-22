using Cinema.LoadBalancer;
using Consul;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Yarp.ReverseProxy.Configuration;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80, o => o.Protocols = HttpProtocols.Http2);
});

var consulHost = builder.Configuration["Consul:Host"] ?? "localhost";
var consulPort = int.Parse(builder.Configuration["Consul:Port"] ?? "8500");

builder.Services.AddSingleton<IConsulClient>(_ => new ConsulClient(config =>
{
    config.Address = new Uri($"http://{consulHost}:{consulPort}");
}));

builder.Services.AddSingleton<IProxyConfigProvider, ConsulProxyConfigProvider>();

builder.Services.AddReverseProxy();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapReverseProxy();

app.Run();
