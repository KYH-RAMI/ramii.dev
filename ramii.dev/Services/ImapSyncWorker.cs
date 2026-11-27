using ramii.dev.Data.Services;

namespace ramii.dev.Services;

public sealed class ImapSyncWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<ImapSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var enabled = configuration.GetValue<bool>(
            "Email:Imap:Enabled");

        var autoSync = configuration.GetValue<bool>(
            "Email:Imap:AutoSyncEnabled");

        var runOnce = configuration.GetValue<bool>(
            "Email:Imap:RunOnce");

        if (!enabled)
        {
            logger.LogInformation("IMAP worker is disabled.");
            return;
        }

        if (environment.IsDevelopment())
        {
            if (runOnce)
            {
                await SyncAsync(stoppingToken);
            }

            return;
        }

        if (!autoSync)
            return;

        logger.LogInformation("IMAP worker started.");

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(60));

        do
        {
            try
            {
                await SyncAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "IMAP worker failed: {ErrorType}.",
                    ex.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SyncAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var imapService = scope.ServiceProvider
            .GetRequiredService<ImapService>();

        await imapService.SyncBackstageMailboxAsync(
            cancellationToken);
    }
}