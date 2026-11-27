namespace Sirh.Api.Middleware;

/// <summary>
/// Appends defensive HTTP security headers to every API response.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var httpContext = (HttpContext)state;
            var headers = httpContext.Response.Headers;

            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("X-Frame-Options", "DENY");
            headers.TryAdd("Referrer-Policy", "no-referrer");
            headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=(), usb=()");
            headers.TryAdd("X-Permitted-Cross-Domain-Policies", "none");
            headers.TryAdd("Cross-Origin-Resource-Policy", "same-origin");

            var path = httpContext.Request.Path;
            if (!path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase) &&
                !path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase))
            {
                headers.TryAdd("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");
            }

            return Task.CompletedTask;
        }, context);

        await next(context);
    }
}
