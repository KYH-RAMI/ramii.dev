using ramii.dev.Core.Interfaces;

namespace ramii.dev.Services;

public sealed class EmailOutboxWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<EmailOutboxWorker> logger) : BackgroundService
{
    private readonly string _workerId = Guid.NewGuid().ToString();

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("Email:Smtp:Enabled"))
        {
            logger.LogInformation("SMTP worker is disabled.");
            return;
        }

        logger.LogInformation("SMTP worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();

                var contactService = scope.ServiceProvider
                    .GetRequiredService<IContactService>();

                var emailService = scope.ServiceProvider
                    .GetRequiredService<IEmailService>();

                var item = await contactService
                    .ReserveForProcessingAsync(_workerId);

                if (item is not null &&
                    !string.IsNullOrWhiteSpace(item.ReservationToken))
                {
                    stoppingToken.ThrowIfCancellationRequested();

                    await emailService.SendOutboxItemAsync(
                        item.Id,
                        item.ReservationToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "SMTP worker failed: {ErrorType}",
                    ex.GetType().Name);
            }

            await Task.Delay(
                TimeSpan.FromSeconds(5),
                stoppingToken);
        }
    }
}