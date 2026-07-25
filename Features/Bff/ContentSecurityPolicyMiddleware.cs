using Microsoft.Extensions.Options;

namespace Plume.Features.Bff;

/// <summary>PLUME-SEC-001 — default Content-Security-Policy for Plume HTML / BFF surfaces.</summary>
public sealed class ContentSecurityPolicyMiddleware(
    RequestDelegate next,
    IOptions<KitharaOptions> kitharaOptions)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var policy = BuildPolicy(kitharaOptions.Value);
        context.Response.OnStarting(static state =>
        {
            var (http, csp) = ((HttpContext, string))state!;
            var headers = http.Response.Headers;
            headers["Content-Security-Policy"] = csp;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["X-Frame-Options"] = "DENY";
            return Task.CompletedTask;
        }, (context, policy));

        await next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Self-hosted Vue islands + same-origin BFF. No third-party script CDNs.
    /// <c>media-src</c> adds <see cref="KitharaOptions.PublicBaseUrl"/> when streams are cross-origin
    /// (split-port Compose). <c>img-src https:</c> covers Magpie/YouTube (and future) artwork URLs.
    /// </summary>
    public static string BuildPolicy(KitharaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var mediaSrc = "'self' blob:";
        var publicBase = options.PublicBaseUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(publicBase)
            && Uri.TryCreate(publicBase, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            mediaSrc += " " + uri.GetLeftPart(UriPartial.Authority);
        }

        return "default-src 'self'; "
            + "base-uri 'self'; "
            + "form-action 'self'; "
            + "frame-ancestors 'none'; "
            + "object-src 'none'; "
            + "script-src 'self'; "
            + "style-src 'self' 'unsafe-inline'; "
            + "img-src 'self' data: https:; "
            + "connect-src 'self'; "
            + $"media-src {mediaSrc}; "
            + "font-src 'self'";
    }
}
