using System.Net;
using System.Net.Sockets;

namespace KonkoorHelp.Api.Services.Integrations;

public sealed record WebhookResolvedTarget(
    Uri Uri,
    string Host,
    int Port,
    IReadOnlyList<IPAddress> Addresses);

/// <summary>SSRF guards for outbound webhook destinations (IPv4/IPv6, DNS validation).</summary>
public sealed class WebhookUrlSafety(IWebhookDnsResolver dns)
{
    private static readonly string[] BlockedHostNames =
    [
        "metadata.google.internal",
        "metadata.goog",
        "kubernetes.default",
        "kubernetes.default.svc",
    ];

    public Task EnsureSafeAsync(string url, CancellationToken cancellationToken) =>
        ResolveValidatedAsync(url, cancellationToken);

    public async Task<WebhookResolvedTarget> ResolveValidatedAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("URL وب‌هوک نامعتبر است.");

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            throw new InvalidOperationException("فقط HTTP/HTTPS مجاز است.");

        if (uri.Scheme == Uri.UriSchemeHttp
            && !IsLoopbackHostName(uri.Host)
            && !string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Host, "[::1]", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("در محیط غیرمحلی فقط HTTPS مجاز است.");
        }

        if (IsLoopbackHostName(uri.Host)
            || string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, "[::1]", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, "0.0.0.0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("مقصد محلی یا حلقه‌ای برای وب‌هوک مجاز نیست.");
        }

        if (BlockedHostNames.Any(item => string.Equals(item, uri.Host, StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith("." + item, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("مقصد وب‌هوک مجاز نیست.");
        }

        var port = uri.IsDefaultPort
            ? uri.Scheme == Uri.UriSchemeHttps ? 443 : 80
            : uri.Port;

        if (uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
        {
            if (!IPAddress.TryParse(uri.IdnHost.Trim('[', ']'), out var literal))
                throw new InvalidOperationException("آدرس IP وب‌هوک نامعتبر است.");
            EnsurePublicUnicast(literal);
            return new WebhookResolvedTarget(uri, uri.IdnHost, port, [Normalize(literal)]);
        }

        IPAddress[] addresses;
        try
        {
            addresses = await dns.ResolveAsync(uri.IdnHost, cancellationToken);
        }
        catch (Exception)
        {
            throw new InvalidOperationException("نام میزبان وب‌هوک قابل حل نیست.");
        }

        if (addresses is null || addresses.Length == 0)
            throw new InvalidOperationException("نام میزبان وب‌هوک قابل حل نیست.");

        var normalized = new List<IPAddress>(addresses.Length);
        foreach (var address in addresses)
        {
            var n = Normalize(address);
            EnsurePublicUnicast(n);
            if (!normalized.Any(item => item.Equals(n)))
                normalized.Add(n);
        }

        if (normalized.Count == 0)
            throw new InvalidOperationException("هیچ آدرس عمومی معتبری برای وب‌هوک یافت نشد.");

        return new WebhookResolvedTarget(uri, uri.IdnHost, port, normalized);
    }

    public static void EnsurePublicUnicast(IPAddress address)
    {
        address = Normalize(address);

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            if (IsBlockedIpv4(bytes))
                throw new InvalidOperationException("مقصد خصوصی/رزرو شده برای وب‌هوک مجاز نیست.");
            return;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (IPAddress.IsLoopback(address)
                || address.IsIPv6LinkLocal
                || address.IsIPv6Multicast
                || address.IsIPv6SiteLocal
                || IsUniqueLocalIpv6(address)
                || address.Equals(IPAddress.IPv6Any)
                || address.Equals(IPAddress.IPv6None))
            {
                throw new InvalidOperationException("مقصد خصوصی/رزرو شده برای وب‌هوک مجاز نیست.");
            }

            return;
        }

        throw new InvalidOperationException("خانواده آدرس وب‌هوک پشتیبانی نمی‌شود.");
    }

    public static IPAddress Normalize(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            return address.MapToIPv4();
        return address;
    }

    private static bool IsLoopbackHostName(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase);

    private static bool IsUniqueLocalIpv6(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 16 && (bytes[0] & 0xfe) == 0xfc;
    }

    private static bool IsBlockedIpv4(byte[] b)
    {
        if (b[0] == 0) return true;
        if (b[0] == 10) return true;
        if (b[0] == 127) return true;
        if (b[0] == 169 && b[1] == 254) return true;
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
        if (b[0] == 192 && b[1] == 168) return true;
        if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return true;
        if (b[0] == 192 && b[1] == 0 && (b[2] == 0 || b[2] == 2)) return true;
        if (b[0] == 198 && b[1] == 51 && b[2] == 100) return true;
        if (b[0] == 203 && b[1] == 0 && b[2] == 113) return true;
        if (b[0] == 198 && b[1] >= 18 && b[1] <= 19) return true;
        if (b[0] >= 224) return true;
        if (b[0] == 255 && b[1] == 255 && b[2] == 255 && b[3] == 255) return true;
        return false;
    }
}
