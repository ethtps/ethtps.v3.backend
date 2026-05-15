using System.Threading.RateLimiting;
using ETHTPS.API.Options;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ETHTPS.API.RateLimiting;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, WebApplicationBuilder _)
    {
        services.AddRateLimiter(limiterOptions =>
        {
            limiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiterOptions.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers["Retry-After"] = "60";
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsync(
                    """{"error":"Rate limit exceeded. Try again later."}""", ct);
            };

            // Single global limiter: reads ApiKeyHash set by ApiKeyMiddleware
            limiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var opts = httpContext.RequestServices
                    .GetRequiredService<IOptions<ApiOptions>>().Value;

                var apiKeyHash = httpContext.Items["ApiKeyHash"] as string;
                if (apiKeyHash is not null)
                {
                    return RateLimitPartition.GetSlidingWindowLimiter(
                        $"apikey:{apiKeyHash}",
                        _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = opts.ApiKeyRateLimitPerMinute,
                            Window = TimeSpan.FromMinutes(1),
                            SegmentsPerWindow = 6,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0
                        });
                }

                var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                var ip = forwardedFor ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetSlidingWindowLimiter(
                    $"anon:{ip}",
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = opts.AnonymousRateLimitPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });
        });

        return services;
    }
}
