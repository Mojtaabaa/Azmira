using System.Net;
using System.Net.Security;
using System.Net.Sockets;

namespace KonkoorHelp.Api.Services.Integrations;

public static class WebhookSocketsHttpHandlerFactory
{
    public static SocketsHttpHandler Create(IWebhookSocketConnector connector)
    {
        ArgumentNullException.ThrowIfNull(connector);

        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            // Never reuse sockets: a pooled connection would skip ConnectCallback and could
            // ignore a newer per-request DNS pin for the same hostname.
            PooledConnectionLifetime = TimeSpan.Zero,
            MaxConnectionsPerServer = 1,
            ConnectCallback = (context, cancellationToken) =>
                ConnectPinnedAsync(connector, context, cancellationToken),
            // Default certificate validation stays enabled. SocketsHttpHandler uses
            // DnsEndPoint.Host (original hostname) for TLS SNI and cert name checks.
            SslOptions = new SslClientAuthenticationOptions(),
        };
    }

    internal static async ValueTask<Stream> ConnectPinnedAsync(
        IWebhookSocketConnector connector,
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var pin = WebhookOutboundPin.Active
            ?? throw new InvalidOperationException("اتصال وب‌هوک بدون پین DNS مجاز نیست.");

        if (!WebhookOutboundPin.HostMatches(pin.Host, context.DnsEndPoint.Host))
        {
            throw new InvalidOperationException("پین اتصال وب‌هوک با میزبان درخواست هم‌خوانی ندارد.");
        }

        if (pin.Port != context.DnsEndPoint.Port)
        {
            throw new InvalidOperationException("پین اتصال وب‌هوک با پورت درخواست هم‌خوانی ندارد.");
        }

        if (pin.Addresses.Count == 0)
            throw new InvalidOperationException("پین اتصال وب‌هوک بدون آدرس معتبر است.");

        Exception? last = null;
        foreach (var address in pin.Addresses)
        {
            try
            {
                return await connector.ConnectAsync(address, context.DnsEndPoint.Port, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                last = error;
            }
        }

        throw last ?? new InvalidOperationException("اتصال به آدرس‌های تأییدشده وب‌هوک ممکن نشد.");
    }
}
