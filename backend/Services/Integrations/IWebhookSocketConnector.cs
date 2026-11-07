using System.Net;
using System.Net.Sockets;

namespace KonkoorHelp.Api.Services.Integrations;

public interface IWebhookSocketConnector
{
    ValueTask<Stream> ConnectAsync(IPAddress address, int port, CancellationToken cancellationToken);
}

public sealed class TcpWebhookSocketConnector : IWebhookSocketConnector
{
    public async ValueTask<Stream> ConnectAsync(IPAddress address, int port, CancellationToken cancellationToken)
    {
        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
        };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
