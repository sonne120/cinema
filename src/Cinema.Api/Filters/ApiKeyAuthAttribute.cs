using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Cinema.Api.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ApiKeyAuthAttribute : Attribute, IAsyncActionFilter
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
        {
            context.Result = new UnauthorizedObjectResult("API Key is missing");
            return;
        }

        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var apiKey = configuration.GetValue<string>("ApiKey");

        if (string.IsNullOrEmpty(apiKey) || !apiKey.Equals(extractedApiKey))
        {
            context.Result = new UnauthorizedObjectResult("Invalid API Key");
            return;
        }

        await next();
    }
}
