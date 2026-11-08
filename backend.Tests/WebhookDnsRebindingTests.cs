using System.Net;
using System.Net.Sockets;
using System.Text;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KonkoorHelp.Tests;

public class WebhookDnsRebindingTests
{
    private static readonly IPAddress PublicV4 = IPAddress.Parse("1.1.1.1");
    private static readonly IPAddress PublicV4Alt = IPAddress.Parse("8.8.8.8");
    private static readonly IPAddress PrivateV4 = IPAddress.Parse("10.0.0.5");
    private static readonly IPAddress PrivateV6 = IPAddress.Parse("fc00::1");
    private static readonly IPAddress PublicV6 = IPAddress.Parse("2606:4700:4700::1111");

    [Fact]
    public async Task Resolve_public_dns_address_succeeds()
    {
        var dns = new FakeDnsResolver();
        dns.Set("hooks.example.test", PublicV4);
        var safety = new WebhookUrlSafety(dns);

        var target = await safety.ResolveValidatedAsync("https://hooks.example.test/hook", CancellationToken.None);

        Assert.Equal("hooks.example.test", target.Host);
        Assert.Equal(443, target.Port);
        Assert.Equal([PublicV4], target.Addresses);
    }

    [Fact]
    public async Task Resolve_rejects_private_ipv4_dns_answer()
    {
        var dns = new FakeDnsResolver();
        dns.Set("evil.example.test", PrivateV4);
        var safety = new WebhookUrlSafety(dns);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            safety.ResolveValidatedAsync("https://evil.example.test/hook", CancellationToken.None));
        Assert.Contains("خصوصی", error.Message);
    }

    [Fact]
    public async Task Resolve_rejects_private_ipv6_dns_answer()
    {
        var dns = new FakeDnsResolver();
        dns.Set("evil6.example.test", PrivateV6);
        var safety = new WebhookUrlSafety(dns);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            safety.ResolveValidatedAsync("https://evil6.example.test/hook", CancellationToken.None));
    }

    [Fact]
    public async Task Resolve_rejects_mixed_public_and_private_dns_answers()
    {
        var dns = new FakeDnsResolver();
        dns.Set("mixed.example.test", PublicV4, PrivateV4, PublicV6);
        var safety = new WebhookUrlSafety(dns);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            safety.ResolveValidatedAsync("https://mixed.example.test/hook", CancellationToken.None));
    }

    [Fact]
    public async Task Resolve_rejects_ipv4_mapped_private_ipv6()
    {
        var dns = new FakeDnsResolver();
        dns.Set("mapped.example.test", IPAddress.Parse("::ffff:10.1.2.3"));
        var safety = new WebhookUrlSafety(dns);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            safety.ResolveValidatedAsync("https://mapped.example.test/hook", CancellationToken.None));
    }

    [Fact]
    public async Task Resolve_dns_failure_fails_closed()
    {
        var dns = new FakeDnsResolver { ThrowOnResolve = true };
        var safety = new WebhookUrlSafety(dns);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            safety.ResolveValidatedAsync("https://missing.example.test/hook", CancellationToken.None));
        Assert.Contains("قابل حل نیست", error.Message);
    }

    [Fact]
    public async Task Resolve_empty_dns_answer_fails_closed()
    {
        var dns = new FakeDnsResolver();
        dns.Set("empty.example.test");
        var safety = new WebhookUrlSafety(dns);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            safety.ResolveValidatedAsync("https://empty.example.test/hook", CancellationToken.None));
    }

    [Fact]
    public void Handler_disables_redirects_and_keeps_default_cert_validation()
    {
        var handler = WebhookSocketsHttpHandlerFactory.Create(new RecordingSocketConnector());
        Assert.False(handler.AllowAutoRedirect);
        Assert.Null(handler.SslOptions.RemoteCertificateValidationCallback);
        Assert.Equal(TimeSpan.Zero, handler.PooledConnectionLifetime);
    }

    [Fact]
    public async Task Connection_uses_only_validated_ip_and_preserves_hostname()
    {
        var connector = new RecordingSocketConnector();
        using var handler = WebhookSocketsHttpHandlerFactory.Create(connector);
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };

        using (WebhookOutboundPin.Begin(new WebhookOutboundPin.PinState(
                   "hooks.example.test",
                   80,
                   [PublicV4],
                   new Uri("http://hooks.example.test/hook"))))
        {
            using var response = await client.PostAsync(
                "http://hooks.example.test/hook",
                new StringContent("""{"ok":true}""", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.Equal([PublicV4], connector.ConnectedAddresses);
        Assert.All(connector.ConnectedPorts, port => Assert.Equal(80, port));
        Assert.NotNull(connector.LastRequestHostHeader);
        Assert.Equal("hooks.example.test", connector.LastRequestHostHeader);
    }

    [Fact]
    public async Task Connection_without_pin_fails_closed()
    {
        var connector = new RecordingSocketConnector();
        using var handler = WebhookSocketsHttpHandlerFactory.Create(connector);
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetAsync("http://hooks.example.test/"));
        Assert.Contains("پین", error.Message);
        Assert.Empty(connector.ConnectedAddresses);
    }

    [Fact]
    public async Task Connection_rejects_host_mismatch_against_pin()
    {
        var connector = new RecordingSocketConnector();
        using var handler = WebhookSocketsHttpHandlerFactory.Create(connector);
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };

        using (WebhookOutboundPin.Begin(new WebhookOutboundPin.PinState(
                   "hooks.example.test",
                   80,
                   [PublicV4],
                   new Uri("http://hooks.example.test/"))))
        {
            var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
                client.GetAsync("http://other.example.test/"));
            Assert.Contains("هم‌خوانی", error.Message);
        }

        Assert.Empty(connector.ConnectedAddresses);
    }

    [Fact]
    public async Task Delivery_rejects_when_dns_rebinding_to_private_between_register_and_delivery()
    {
        var dns = new FakeDnsResolver();
        dns.Set("rebind.example.test", PublicV4);
        var connector = new RecordingSocketConnector();

        await using var sp = BuildSp(dns, connector);
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var webhooks = scope.ServiceProvider.GetRequiredService<WebhookService>();

        var user = new UserAccount
        {
            Id = 9601,
            Username = "rebind",
            NormalizedUsername = "rebind",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        var ws = Guid.NewGuid();
        db.Workspaces.Add(new Workspace
        {
            Id = ws,
            Type = WorkspaceType.Personal,
            Name = "rebind",
            OwnerUserId = user.Id,
            Status = WorkspaceStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await webhooks.CreateEndpointAsync(ws, user.Id, "https://rebind.example.test/hook", ["LevelUp"], CancellationToken.None);
        await webhooks.EnqueueForEventAsync(ws, Guid.NewGuid(), WebhookEventTypes.LevelUp, new { level = 2 }, CancellationToken.None);

        dns.Set("rebind.example.test", PrivateV4);

        var processed = await webhooks.ProcessPendingAsync(CancellationToken.None);
        Assert.Equal(1, processed);

        var delivery = await db.WebhookDeliveries.SingleAsync();
        Assert.Equal(WebhookDeliveryStatus.DeadLetter, delivery.Status);
        Assert.Contains("خصوصی", delivery.LastError);
        Assert.Empty(connector.ConnectedAddresses);
    }

    [Fact]
    public async Task Delivery_pins_validated_public_ip_with_original_https_host()
    {
        var dns = new FakeDnsResolver();
        dns.Set("ok.example.test", PublicV4Alt);
        var recording = new PinCapturingHandler();

        await using var sp = BuildSp(dns, new RecordingSocketConnector(), recording);
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var webhooks = scope.ServiceProvider.GetRequiredService<WebhookService>();

        var user = new UserAccount
        {
            Id = 9602,
            Username = "pinok",
            NormalizedUsername = "pinok",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        var ws = Guid.NewGuid();
        db.Workspaces.Add(new Workspace
        {
            Id = ws,
            Type = WorkspaceType.Personal,
            Name = "pin",
            OwnerUserId = user.Id,
            Status = WorkspaceStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await webhooks.CreateEndpointAsync(ws, user.Id, "https://ok.example.test/hook", ["LevelUp"], CancellationToken.None);
        await webhooks.EnqueueForEventAsync(ws, Guid.NewGuid(), WebhookEventTypes.LevelUp, new { level = 4 }, CancellationToken.None);

        var processed = await webhooks.ProcessPendingAsync(CancellationToken.None);
        Assert.Equal(1, processed);

        var delivery = await db.WebhookDeliveries.SingleAsync();
        Assert.Equal(WebhookDeliveryStatus.Succeeded, delivery.Status);
        Assert.NotNull(recording.CapturedPin);
        Assert.Equal("ok.example.test", recording.CapturedPin.Host);
        Assert.Equal(443, recording.CapturedPin.Port);
        Assert.Equal([PublicV4Alt], recording.CapturedPin.Addresses.ToArray());
        Assert.Equal("ok.example.test", recording.RequestHost);
        Assert.Equal(Uri.UriSchemeHttps, recording.RequestScheme);
    }

    private static ServiceProvider BuildSp(
        IWebhookDnsResolver dns,
        IWebhookSocketConnector connector,
        HttpMessageHandler? deliveryHandler = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => TestDbOptions.UseInMemory(options, Guid.NewGuid().ToString("N")));
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddSingleton(Options.Create(new IntegrationsOptions
        {
            WebhookProtectionKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
        }));
        services.AddSingleton<WebhookSecretProtector>();
        services.AddSingleton(dns);
        services.AddSingleton(connector);
        services.AddSingleton<WebhookUrlSafety>();
        services.AddScoped<WebhookService>();
        var http = services.AddHttpClient(nameof(WebhookService), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        if (deliveryHandler is not null)
        {
            http.ConfigurePrimaryHttpMessageHandler(() => deliveryHandler);
        }
        else
        {
            http.ConfigurePrimaryHttpMessageHandler(sp =>
                WebhookSocketsHttpHandlerFactory.Create(sp.GetRequiredService<IWebhookSocketConnector>()));
        }

        services.AddLogging();
        return services.BuildServiceProvider();
    }

    private sealed class FakeDnsResolver : IWebhookDnsResolver
    {
        private readonly Dictionary<string, IPAddress[]> _map = new(StringComparer.OrdinalIgnoreCase);
        public bool ThrowOnResolve { get; set; }

        public void Set(string host, params IPAddress[] addresses) =>
            _map[host] = addresses;

        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            if (ThrowOnResolve)
                throw new SocketException((int)SocketError.HostNotFound);

            if (!_map.TryGetValue(host, out var addresses))
                throw new SocketException((int)SocketError.HostNotFound);

            return Task.FromResult(addresses);
        }
    }

    /// <summary>
    /// Records the IP HttpClient asked to connect to, then serves a minimal HTTP response
    /// over a loopback exchange so no external network/DNS is required.
    /// </summary>
    private sealed class RecordingSocketConnector : IWebhookSocketConnector
    {
        private readonly List<IPAddress> _addresses = [];
        private readonly List<int> _ports = [];
        private Task? _serverTask;

        public IReadOnlyList<IPAddress> ConnectedAddresses => _addresses;
        public IReadOnlyList<int> ConnectedPorts => _ports;
        public string? LastRequestHostHeader { get; private set; }

        public async ValueTask<Stream> ConnectAsync(IPAddress address, int port, CancellationToken cancellationToken)
        {
            _addresses.Add(address);
            _ports.Add(port);

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var localPort = ((IPEndPoint)listener.LocalEndpoint).Port;

            _serverTask = Task.Run(async () =>
            {
                try
                {
                    using var server = await listener.AcceptTcpClientAsync(CancellationToken.None);
                    await using var stream = server.GetStream();
                    var buffer = new byte[8192];
                    var read = await stream.ReadAsync(buffer, CancellationToken.None);
                    var requestText = Encoding.ASCII.GetString(buffer, 0, Math.Max(0, read));
                    foreach (var line in requestText.Split("\r\n"))
                    {
                        if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                        {
                            LastRequestHostHeader = line["Host:".Length..].Trim();
                            break;
                        }
                    }

                    var response = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok"u8.ToArray();
                    await stream.WriteAsync(response, CancellationToken.None);
                    await stream.FlushAsync(CancellationToken.None);
                }
                finally
                {
                    listener.Stop();
                }
            }, CancellationToken.None);

            var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, localPort, cancellationToken);
            return new NetworkStream(client.Client, ownsSocket: true);
        }
    }

    /// <summary>
    /// Observes the outbound pin + request URI without performing real TLS (delivery success path).
    /// </summary>
    private sealed class PinCapturingHandler : HttpMessageHandler
    {
        public WebhookOutboundPin.PinState? CapturedPin { get; private set; }
        public string? RequestHost { get; private set; }
        public string? RequestScheme { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedPin = WebhookOutboundPin.Active;
            RequestHost = request.RequestUri?.IdnHost;
            RequestScheme = request.RequestUri?.Scheme;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok"),
            });
        }
    }
}
