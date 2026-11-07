namespace KonkoorHelp.Api.Services.Integrations;

public sealed class WebhookDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<WebhookDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var webhooks = scope.ServiceProvider.GetRequiredService<WebhookService>();
                var processed = await webhooks.ProcessPendingAsync(stoppingToken);
                await Task.Delay(processed > 0 ? 500 : 2000, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogError(error, "Webhook dispatcher loop failed");
                await Task.Delay(4000, stoppingToken);
            }
        }
    }
}
