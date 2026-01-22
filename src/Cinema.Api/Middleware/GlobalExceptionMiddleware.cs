using System.Text.Json;
using Cinema.Domain.Common.Errors;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Cinema.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

        var problemDetails = CreateProblemDetails(context, exception);

        context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, options));
    }

    private ProblemDetails CreateProblemDetails(HttpContext context, Exception exception)
    {
        return exception switch
        {
            ValidationException validationEx => CreateValidationProblemDetails(context, validationEx),
            ArgumentException argEx => CreateBadRequestProblemDetails(context, argEx),
            KeyNotFoundException notFoundEx => CreateNotFoundProblemDetails(context, notFoundEx),
            UnauthorizedAccessException unauthorizedEx => CreateUnauthorizedProblemDetails(context, unauthorizedEx),
            InvalidOperationException invalidOpEx => CreateConflictProblemDetails(context, invalidOpEx),
            _ => CreateInternalServerErrorProblemDetails(context, exception)
        };
    }

    private ValidationProblemDetails CreateValidationProblemDetails(
        HttpContext context,
        ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Failed",
            Detail = "One or more validation errors occurred.",
            Instance = context.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
        };
    }

    private ProblemDetails CreateBadRequestProblemDetails(
        HttpContext context,
        ArgumentException exception)
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
            Detail = exception.Message,
            Instance = context.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
        };
    }

    private ProblemDetails CreateNotFoundProblemDetails(
        HttpContext context,
        KeyNotFoundException exception)
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Not Found",
            Detail = exception.Message,
            Instance = context.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4"
        };
    }

    private ProblemDetails CreateUnauthorizedProblemDetails(
        HttpContext context,
        UnauthorizedAccessException exception)
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = "You are not authorized to access this resource.",
            Instance = context.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7235#section-3.1"
        };
    }

    private ProblemDetails CreateConflictProblemDetails(
        HttpContext context,
        InvalidOperationException exception)
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflict",
            Detail = exception.Message,
            Instance = context.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8"
        };
    }

    private ProblemDetails CreateInternalServerErrorProblemDetails(
        HttpContext context,
        Exception exception)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Detail = _environment.IsDevelopment()
                ? exception.Message
                : "An unexpected error occurred. Please try again later.",
            Instance = context.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
        };

        if (_environment.IsDevelopment())
        {
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
            problemDetails.Extensions["innerException"] = exception.InnerException?.Message;
        }

        return problemDetails;
    }
}

public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
