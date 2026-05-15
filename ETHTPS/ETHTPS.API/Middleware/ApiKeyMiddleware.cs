using ETHTPS.API.Services;

namespace ETHTPS.API.Middleware;

public class ApiKeyMiddleware(RequestDelegate next, ApiKeyService apiKeyService)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var rawKey = context.Request.Headers["X-Api-Key"].FirstOrDefault()
                     ?? context.Request.Query["apiKey"].FirstOrDefault();

        if (rawKey is not null)
        {
            var valid = await apiKeyService.ValidateAsync(rawKey, context.RequestAborted);
            if (!valid)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid or expired API key.");
                return;
            }
            context.Items["ApiKeyHash"] = ApiKeyService.ComputeHash(rawKey);
        }

        await next(context);
    }
}
