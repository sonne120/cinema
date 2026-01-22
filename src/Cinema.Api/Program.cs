using System.Text;
using System.Threading.RateLimiting;
using Cinema.Api.Middleware;
using Cinema.Api.Services;
using Cinema.Application;
using Cinema.Infrastructure;
using Cinema.Infrastructure.Authentication;
using Cinema.Infrastructure.Persistence.Write;
using Cinema.Infrastructure.ServiceDiscovery;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080, o => o.Protocols = HttpProtocols.Http1AndHttp2);
});

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddGrpc();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Cinema DDD CQRS API",
        Version = "v1",
        Description = "Clean Architecture"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ManagerOrAdmin", policy => policy.RequireRole("Manager", "Admin"));
    options.AddPolicy("StaffOrAbove", policy => policy.RequireRole("Staff", "Manager", "Admin"));
});

builder.Services.AddApplication();

builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

builder.Services.AddInfrastructure(builder.Configuration);

var serviceHost = builder.Configuration["Consul:ServiceHost"] ?? "localhost";
var servicePort = int.Parse(builder.Configuration["Consul:ServicePort"] ?? "8080");
builder.Services.AddConsulServiceRegistration(
    builder.Configuration,
    serviceName: "cinema-api",
    serviceHost: serviceHost,
    servicePort: servicePort,
    tags: new[] { "api", "cinema", "dotnet", "write" });

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000", "http://localhost:5173", "http://cinema-gateway:8080" };

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });

    options.AddPolicy("Development", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 5;
    });

    options.AddSlidingWindowLimiter("sliding", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.SegmentsPerWindow = 6;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 5;
    });

    options.AddTokenBucketLimiter("token", opt =>
    {
        opt.TokenLimit = 100;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 5;
        opt.ReplenishmentPeriod = TimeSpan.FromSeconds(10);
        opt.TokensPerPeriod = 20;
        opt.AutoReplenishment = true;
    });

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            });
    });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/problem+json";

        var problemDetails = new
        {
            type = "https://tools.ietf.org/html/rfc6585#section-4",
            title = "Too Many Requests",
            status = 429,
            detail = "Rate limit exceeded. Please try again later.",
            instance = context.HttpContext.Request.Path.Value
        };

        await context.HttpContext.Response.WriteAsJsonAsync(problemDetails, token);
    };
});

var app = builder.Build();

app.UseGlobalExceptionHandler();

app.UseCorrelationId();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

app.UseCors();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapGrpcService<CinemaWriteGrpcService>();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
    if (dbContext.Database.IsSqlServer())
    {
        dbContext.Database.Migrate();
    }
}

app.Run();

public partial class Program { }

namespace Cinema.Application
{
    public class AssemblyReference { }
}
