using System.Globalization;
using Microsoft.AspNetCore.Routing;

namespace Plume.Features.Bff;

/// <summary>
/// Catch-all BFF proxy mirrors non-auth Kithara REST. The <c>/auth</c> namespace is
/// owned by <see cref="BffAuthEndpoints"/> (plus an explicit <c>/auth/me</c> map) —
/// this constraint keeps those requests from selecting the proxy endpoint at all.
/// </summary>
public sealed class BffNonAuthProxyConstraint : IRouteConstraint
{
    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection)
    {
        if (!values.TryGetValue(routeKey, out var raw) || raw is null)
        {
            return true;
        }

        var path = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
        var trimmed = path.Trim('/');

        // Entire auth tree is explicitly routed — not an open JSON proxy.
        return !trimmed.Equals("auth", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("auth/", StringComparison.OrdinalIgnoreCase);
    }
}
