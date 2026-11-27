using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using ramii.dev.Core.Configuration;
using ramii.dev.Core.Entities;
using ramii.dev.Core.Interfaces;

namespace ramii.dev.Data.Services;

public class EmailService : IEmailService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IContactService _contactService;
    private readonly SmtpSettings _smtpSettings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IDbContextFactory<AppDbContext> factory,
        IContactService contactService,
        IOptions<SmtpSettings> smtpSettings,
        ILogger<EmailService> logger)
    {
        _factory = factory;
        _contactService = contactService;
        _smtpSettings = smtpSettings.Value;
        _logger = logger;
    }

    public async Task<bool> SendOutboxItemAsync(
        int outboxItemId,
        string reservationToken)
    {
        if (!_smtpSettings.Enabled)
        {
            _logger.LogWarning(
                "SMTP is disabled. Cannot send outbox item {OutboxItemId}.",
                outboxItemId);

            return false;
        }

        await using var context = await _factory.CreateDbContextAsync();

        var outbox = await context.OutboxItems
            .Include(o => o.ThreadMessage)
            .ThenInclude(m => m!.Thread)
            .FirstOrDefaultAsync(o => o.Id == outboxItemId);

        if (outbox == null)
        {
            _logger.LogError(
                "Outbox item {OutboxItemId} not found.",
                outboxItemId);

            return false;
        }

        // Validera reservationstoken
        if (string.IsNullOrWhiteSpace(reservationToken) ||
            !string.Equals(
                outbox.ReservationToken,
                reservationToken,
                StringComparison.Ordinal))
        {
            _logger.LogError(
                "Token mismatch for outbox item {OutboxItemId}.",
                outboxItemId);

            return false;
        }

        var message = outbox.ThreadMessage;

        if (message == null)
        {
            _logger.LogError(
                "Message not found for outbox item {OutboxItemId}.",
                outboxItemId);

            return false;
        }

        var sendAttempted = false;

        try
        {
            // Kontrollera autentiseringsuppgifter
            if (string.IsNullOrWhiteSpace(_smtpSettings.Username) ||
                string.IsNullOrWhiteSpace(_smtpSettings.Password))
            {
                throw new InvalidOperationException(
                    "SMTP credentials are missing.");
            }

            // Skapa e-postmeddelande
            var emailMessage = new MimeMessage();

            emailMessage.From.Add(
                new MailboxAddress(
                    _smtpSettings.SenderName,
                    _smtpSettings.SenderAddress));

            emailMessage.ReplyTo.Add(
                new MailboxAddress(
                    _smtpSettings.SenderName,
                    _smtpSettings.SenderAddress));

            emailMessage.To.Add(
                new MailboxAddress(
                    string.Empty,
                    outbox.RecipientEmail));

            emailMessage.Subject =
                $"RE: {message.Thread?.Subject ?? "Svar från ramii.dev"}";

            emailMessage.MessageId = outbox.MessageId;

            // Koppla meddelandet till befintlig konversation
            if (!string.IsNullOrWhiteSpace(message.InReplyToMessageId))
            {
                emailMessage.InReplyTo = message.InReplyToMessageId;
            }

            if (!string.IsNullOrWhiteSpace(message.References))
            {
                emailMessage.References.AddRange(
                    message.References.Split(
                        ' ',
                        StringSplitOptions.RemoveEmptyEntries));
            }

            // Hämta föregående inkommande e-post i samma ärende
            var previousEmail = await context.Messages
                .AsNoTracking()
                .Where(m => m.ThreadId == message.ThreadId &&
                            m.Id < message.Id &&
                            m.Source == MessageSource.EmailIncoming)
                .OrderByDescending(m => m.Id)
                .FirstOrDefaultAsync();

            var emailBody = message.Body;

            if (previousEmail is not null)
            {
                var quotedBody = string.Join(
                    "\n",
                    previousEmail.Body
                        .Replace("\r\n", "\n")
                        .Split('\n')
                        .Select(line => $"> {line}"));

                emailBody += $"\n\n--- Tidigare meddelande ---\n{quotedBody}";
            }

            var bodyBuilder = new BodyBuilder
            {
                TextBody = emailBody
            };

            emailMessage.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();

            // Krypterad SMTP-anslutning
            await client.ConnectAsync(
                _smtpSettings.Host,
                _smtpSettings.Port,
                SecureSocketOptions.StartTls);

            // Obligatorisk autentisering
            await client.AuthenticateAsync(
                _smtpSettings.Username,
                _smtpSettings.Password);

            // Skicka meddelandet
            sendAttempted = true;

            await client.SendAsync(emailMessage);

            // SMTP-servern har accepterat meddelandet
            await _contactService.UpdateOutboxItemStatusAsync(
                outboxItemId,
                OutboxItemStatus.Sent,
                reservationToken);

            _logger.LogInformation(
                "Successfully sent outbox item {OutboxItemId}.",
                outboxItemId);

            // Stäng SMTP-anslutningen
            try
            {
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "SMTP disconnect failed: {ErrorType}.",
                    ex.GetType().Name);
            }

            return true;
        }
        catch (Exception ex)
        {
            // Förhindra automatiska omskick vid osäkert resultat
            var status = sendAttempted
                ? OutboxItemStatus.Uncertain
                : OutboxItemStatus.Failed;

            _logger.LogError(
                "Outbox item {OutboxItemId} failed. Error: {ErrorType}.",
                outboxItemId,
                ex.GetType().Name);

            await _contactService.UpdateOutboxItemStatusAsync(
                outboxItemId,
                status,
                reservationToken,
                ex.GetType().Name);

            return false;
        }
    }
}