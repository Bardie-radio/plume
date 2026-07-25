namespace Plume.Features.Bff;

/// <summary>PLUME-SEC-001 — default Content-Security-Policy for Plume HTML / BFF surfaces.</summary>
public sealed class ContentSecurityPolicyMiddleware(RequestDelegate next)
{
    // Self-hosted Vue islands + same-origin BFF. No third-party script/style CDNs in MVP.
    private const string Policy =
        "default-src 'self'; "
        + "base-uri 'self'; "
        + "form-action 'self'; "
        + "frame-ancestors 'none'; "
        + "object-src 'none'; "
        + "script-src 'self'; "
        + "style-src 'self' 'unsafe-inline'; "
        + "img-src 'self' data:; "
        + "connect-src 'self'; "
        + "media-src 'self' blob:; "
        + "font-src 'self'";

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var http = (HttpContext)state!;
            var headers = http.Response.Headers;
            headers["Content-Security-Policy"] = Policy;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["X-Frame-Options"] = "DENY";
            return Task.CompletedTask;
        }, context);

        await next(context).ConfigureAwait(false);
    }
}
