using System.Net;

namespace KonkoorHelp.Api.Services.Integrations;

/// <summary>
/// Request-scoped pin of validated destination IPs for webhook ConnectCallback.
/// Not a global hostname cache — each Begin() applies only to the current async flow.
/// </summary>
public static class WebhookOutboundPin
{
    private static readonly AsyncLocal<PinState?> Current = new();

    public sealed record PinState(
        string Host,
        int Port,
        IReadOnlyList<IPAddress> Addresses,
        Uri OriginalUri);

    public static PinState? Active => Current.Value;

    public static IDisposable Begin(PinState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Addresses.Count == 0)
            throw new InvalidOperationException("پین اتصال وب‌هوک بدون آدرس معتبر مجاز نیست.");

        var prior = Current.Value;
        Current.Value = state;
        return new Reset(prior);
    }

    public static bool HostMatches(string pinnedHost, string requestHost)
    {
        if (string.Equals(pinnedHost, requestHost, StringComparison.OrdinalIgnoreCase))
            return true;

        // Uri may present IPv6 without brackets while DnsEndPoint may differ slightly.
        if (IPAddress.TryParse(pinnedHost.Trim('[', ']'), out var a)
            && IPAddress.TryParse(requestHost.Trim('[', ']'), out var b))
        {
            return a.Equals(b);
        }

        return false;
    }

    private sealed class Reset(PinState? prior) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            Current.Value = prior;
            _disposed = true;
        }
    }
}
