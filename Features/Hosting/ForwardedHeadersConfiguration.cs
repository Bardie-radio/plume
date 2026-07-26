using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace Plume.Features.Hosting;

/// <summary>
/// Operator knobs for <see cref="ForwardedHeadersOptions"/> (PLUME-FWD-001).
/// Unset = no proxy. Same knobs as Kithara — set explicitly in Compose behind an edge.
/// </summary>
public static class ForwardedHeadersConfiguration
{
    public static bool IsEnabled(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (ParseBool(
                FirstNonEmpty(
                    configuration["BARDIE_FORWARDED_HEADERS"],
                    configuration["ForwardedHeaders:Enabled"]),
                defaultValue: false))
        {
            return true;
        }

        return FirstNonEmpty(
            configuration["BARDIE_FORWARDED_HEADERS_FORWARD_LIMIT"],
            configuration["ForwardedHeaders:ForwardLimit"],
            configuration["BARDIE_FORWARDED_HEADERS_CLEAR_KNOWN"],
            configuration["ForwardedHeaders:ClearKnown"],
            configuration["BARDIE_FORWARDED_HEADERS_KNOWN_PROXIES"],
            configuration["ForwardedHeaders:KnownProxies"],
            configuration["BARDIE_FORWARDED_HEADERS_KNOWN_NETWORKS"],
            configuration["ForwardedHeaders:KnownNetworks"]) is not null;
    }

    public static void Apply(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!IsEnabled(configuration))
        {
            options.ForwardedHeaders = ForwardedHeaders.None;
            return;
        }

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        var limitRaw = FirstNonEmpty(
            configuration["BARDIE_FORWARDED_HEADERS_FORWARD_LIMIT"],
            configuration["ForwardedHeaders:ForwardLimit"]);
        if (int.TryParse(limitRaw, out var limit) && limit > 0)
        {
            options.ForwardLimit = limit;
        }

        if (ParseBool(
                FirstNonEmpty(
                    configuration["BARDIE_FORWARDED_HEADERS_CLEAR_KNOWN"],
                    configuration["ForwardedHeaders:ClearKnown"]),
                defaultValue: false))
        {
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        }

        foreach (var proxy in SplitList(
                     FirstNonEmpty(
                         configuration["BARDIE_FORWARDED_HEADERS_KNOWN_PROXIES"],
                         configuration["ForwardedHeaders:KnownProxies"])))
        {
            if (IPAddress.TryParse(proxy, out var address))
            {
                options.KnownProxies.Add(address);
            }
        }

        foreach (var cidr in SplitList(
                     FirstNonEmpty(
                         configuration["BARDIE_FORWARDED_HEADERS_KNOWN_NETWORKS"],
                         configuration["ForwardedHeaders:KnownNetworks"])))
        {
            if (System.Net.IPNetwork.TryParse(cidr, out var network))
            {
                options.KnownIPNetworks.Add(network);
            }
        }
    }

    private static bool ParseBool(string? raw, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return raw.Trim() switch
        {
            "1" or "true" or "True" or "TRUE" or "yes" or "YES" => true,
            "0" or "false" or "False" or "FALSE" or "no" or "NO" => false,
            _ => defaultValue,
        };
    }

    private static IEnumerable<string> SplitList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            yield break;
        }

        foreach (var part in raw.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return part;
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
