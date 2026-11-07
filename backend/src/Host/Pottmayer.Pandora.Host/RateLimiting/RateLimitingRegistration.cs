using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Localization.Abstractions;
using Pottmayer.Tars.Web.Http;
using Pottmayer.Tars.Web.Http.Abstractions;

namespace Pottmayer.Pandora.Host.RateLimiting;

/// <summary>
/// ASP.NET Core's rate limiter, with Pandora's three limits: every <c>/api</c> request, the auth
/// endpoints and the Assistant. Counters are per process, in memory: a restart resets them, and a
/// second instance would count on its own.
/// </summary>
/// <remarks>
/// The options are read on each request, not at registration, so configuration layered on later
/// (the integration tests) still applies. The client IP is the one the forwarded headers resolved.
/// </remarks>
internal static class RateLimitingRegistration
{
    public const string RejectedCode = "RateLimit.Exceeded";

    public static IServiceCollection AddPandoraRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingOptions>().BindConfiguration(RateLimitingOptions.SectionName);

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = WriteRejectionAsync;

            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                http.Request.Path.StartsWithSegments("/api")
                    ? Partition(http, o => o.Api, $"api:{UserOrIp(http)}")
                    : RateLimitPartition.GetNoLimiter("none"));

            limiter.AddPolicy(RateLimitPolicies.Auth, http => Partition(http, o => o.Auth, $"auth:{Ip(http)}"));
            limiter.AddPolicy(RateLimitPolicies.Assistant, http => Partition(http, o => o.Assistant, $"assistant:{UserOrIp(http)}"));
        });

        return services;
    }

    private static RateLimitPartition<string> Partition(HttpContext http, Func<RateLimitingOptions, RateLimit> pick, string key)
    {
        var options = http.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
        if (!options.Enabled)
            return RateLimitPartition.GetNoLimiter("disabled");

        var limit = pick(options);
        // The limit is part of the key, so changing it in configuration starts a fresh window.
        return RateLimitPartition.GetFixedWindowLimiter($"{key}:{limit.PermitLimit}/{limit.WindowSeconds}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit.PermitLimit,
            Window = TimeSpan.FromSeconds(limit.WindowSeconds),
            QueueLimit = 0,
        });
    }

    private static string Ip(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string UserOrIp(HttpContext http) =>
        http.User.FindFirstValue("sub") ?? http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? $"ip:{Ip(http)}";

    // The same envelope as every other error, so the client shows the message like any failure.
    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken ct)
    {
        var http = context.HttpContext;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            http.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

        var messages = http.RequestServices.GetRequiredService<IMessageProvider>();
        await http.Response.WriteAsJsonAsync(new HttpErrorResponse
        {
            Success = false,
            ErrorCode = RejectedCode,
            ErrorMessage = messages.Get(RejectedCode, fallback: "Too many requests. Wait a moment and try again."),
        }, ct);
    }
}
