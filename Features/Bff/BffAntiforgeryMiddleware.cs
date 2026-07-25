using Microsoft.AspNetCore.Antiforgery;

namespace Plume.Features.Bff;

/// <summary>
/// PLUME-SEC-002 — require antiforgery on unsafe <c>/bff/*</c> methods (header or form field).
/// Safe methods still mint/store tokens so clients can obtain a request token.
/// </summary>
public sealed class BffAntiforgeryMiddleware(RequestDelegate next, IAntiforgery antiforgery)
{
    public const string HeaderName = "X-CSRF-TOKEN";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/bff", StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var method = context.Request.Method;
        var isUnsafe = HttpMethods.IsPost(method)
            || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method)
            || HttpMethods.IsDelete(method);

        if (isUnsafe)
        {
            // CSRF token endpoint is itself a GET — mutations must present a token.
            try
            {
                await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
            }
            catch (AntiforgeryValidationException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    ok = false,
                    error = "antiforgery_token_invalid",
                }).ConfigureAwait(false);
                return;
            }
        }

        await next(context).ConfigureAwait(false);
    }
}
