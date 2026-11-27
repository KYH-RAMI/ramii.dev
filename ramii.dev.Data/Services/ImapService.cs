using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using ramii.dev.Core.Configuration;
using ramii.dev.Core.Entities;

namespace ramii.dev.Data.Services;

public class ImapService
{
    private const string ContactAddress = "kontakt@ramii.dev";
    private const string FolderName = "Backstage";

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ImapSettings _imapSettings;
    private readonly ILogger<ImapService> _logger;

    public ImapService(
        IDbContextFactory<AppDbContext> factory,
        IOptions<ImapSettings> imapSettings,
        ILogger<ImapService> logger)
    {
        _factory = factory;
        _imapSettings = imapSettings.Value;
        _logger = logger;
    }

    public async Task SyncBackstageMailboxAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_imapSettings.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(_imapSettings.Username) ||
            string.IsNullOrWhiteSpace(_imapSettings.Password))
        {
            _logger.LogError("IMAP credentials are missing.");
            return;
        }

        if (!string.Equals(
                _imapSettings.FolderName,
                FolderName,
                StringComparison.Ordinal))
        {
            _logger.LogError("Invalid IMAP folder configuration.");
            return;
        }

        try
        {
            using var client = new ImapClient();

            await client.ConnectAsync(
                _imapSettings.Host,
                _imapSettings.Port,
                SecureSocketOptions.SslOnConnect,
                cancellationToken);

            await client.AuthenticateAsync(
                _imapSettings.Username,
                _imapSettings.Password,
                cancellationToken);

            var mailbox = await client.GetFolderAsync(
                FolderName,
                cancellationToken);

            await mailbox.OpenAsync(
                FolderAccess.ReadOnly,
                cancellationToken);

            var uids = await mailbox.SearchAsync(
                SearchQuery.All,
                cancellationToken);

            foreach (var uid in uids)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var message = await mailbox.GetMessageAsync(
                    uid,
                    cancellationToken);

                await ProcessIncomingEmailAsync(
                    message,
                    cancellationToken);
            }

            await mailbox.CloseAsync(false, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation(
                "IMAP synchronization completed. Examined {Count} messages.",
                uids.Count);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "IMAP synchronization failed: {ErrorType}.",
                ex.GetType().Name);
        }
    }

    private async Task ProcessIncomingEmailAsync(
        MimeMessage message,
        CancellationToken cancellationToken)
    {
        // Kontrollera mottagaren
        if (!message.To.Mailboxes.Any(address =>
                string.Equals(
                    address.Address,
                    ContactAddress,
                    StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning("Incoming email skipped: recipient mismatch.");
            return;
        }

        // Kräv en entydig avsändare
        var senders = message.From.Mailboxes.ToArray();

        if (senders.Length != 1)
            return;

        var fromAddress = senders[0].Address;
        var messageId = NormalizeMessageId(message.MessageId);

        if (string.IsNullOrWhiteSpace(messageId) ||
            messageId.Length > 255)
        {
            _logger.LogWarning("Incoming email skipped: invalid Message-ID.");
            return;
        }

        // Samla samtliga referenser
        var inReplyTo = NormalizeMessageId(message.InReplyTo);

        var referenceIds = message.References
            .Select(NormalizeMessageId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var references = string.Join(" ", referenceIds);

        if (inReplyTo.Length > 255 ||
            references.Length > 2000 ||
            referenceIds.Any(id => id.Length > 255))
        {
            _logger.LogWarning("Incoming email skipped: oversized headers.");
            return;
        }

        var correlationIds = referenceIds
            .Append(inReplyTo)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (correlationIds.Length == 0)
        {
            _logger.LogWarning("Incoming email skipped: no thread references.");
            return;
        }

        // Stöd befintliga Message-ID med och utan vinkelparenteser
        var lookupIds = correlationIds
            .SelectMany(id => new[] { id, $"<{id}>" })
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // Endast klartext
        var body = message.TextBody?.Trim();

        if (string.IsNullOrWhiteSpace(body) || body.Length > 4000)
        {
            _logger.LogWarning(
                "Incoming email skipped: unsupported or oversized body.");

            return;
        }

        try
        {
            await using var context =
                await _factory.CreateDbContextAsync(cancellationToken);

            var bracketedId = $"<{messageId}>";

            // Kontrollera dubbletter
            var exists = await context.Messages.AnyAsync(
                m => m.MessageId == messageId ||
                     m.MessageId == bracketedId,
                cancellationToken);

            if (exists)
                return;

            // Identifiera samtliga matchande ärenden
            var matches = await context.Messages
                .AsNoTracking()
                .Where(m => m.MessageId != null &&
                            lookupIds.Contains(m.MessageId))
                .Select(m => new { m.ThreadId, m.MessageId })
                .ToListAsync(cancellationToken);

            var threadIds = matches
                .Where(m => correlationIds.Contains(
                    NormalizeMessageId(m.MessageId),
                    StringComparer.Ordinal))
                .Select(m => m.ThreadId)
                .Distinct()
                .ToArray();

            if (threadIds.Length != 1)
            {
                _logger.LogWarning(
                    "Incoming email skipped: unknown or ambiguous thread.");

                return;
            }

            var thread = await context.Threads
                .FirstOrDefaultAsync(
                    t => t.Id == threadIds[0],
                    cancellationToken);

            if (thread == null)
                return;

            // Kontrollera att avsändaren matchar ärendet
            if (!string.Equals(
                    fromAddress,
                    thread.VisitorEmail,
                    StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Incoming email skipped: sender mismatch.");
                return;
            }

            var incomingMessage = new ThreadMessage
            {
                ThreadId = thread.Id,
                Body = body,
                IsFromAdmin = false,
                SentAt = message.Date.UtcDateTime,
                Source = MessageSource.EmailIncoming,
                MessageId = messageId,
                InReplyToMessageId =
                    string.IsNullOrWhiteSpace(inReplyTo)
                        ? null
                        : inReplyTo,
                References =
                    string.IsNullOrWhiteSpace(references)
                        ? null
                        : references,
                IpAddress = null,
                UserAgent = null
            };

            thread.LastActivityAt = DateTime.UtcNow;
            thread.Status = ThreadStatus.Open;

            context.Messages.Add(incomingMessage);

            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Incoming email imported into thread {ThreadId}.",
                thread.Id);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Incoming email import failed: {ErrorType}.",
                ex.GetType().Name);
        }
    }

    private static string NormalizeMessageId(string? value)
    {
        return value?.Trim().Trim('<', '>') ?? string.Empty;
    }
}