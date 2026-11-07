using System.Net;

namespace KonkoorHelp.Api.Services.Integrations;

public interface IWebhookDnsResolver
{
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
}

public sealed class SystemWebhookDnsResolver : IWebhookDnsResolver
{
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) =>
        Dns.GetHostAddressesAsync(host, cancellationToken);
}
