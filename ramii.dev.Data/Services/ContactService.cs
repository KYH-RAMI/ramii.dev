using Microsoft.EntityFrameworkCore;
using ramii.dev.Core.Entities;
using ramii.dev.Core.Interfaces;

namespace ramii.dev.Data.Services;

public class ContactService : IContactService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ContactService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<ContactThread> CreateThreadAsync(string name, string email, string subject, string message)
    {
        await using var context = await _factory.CreateDbContextAsync();

        var thread = new ContactThread
        {
            VisitorName = name,
            VisitorEmail = email,
            Subject = subject,
            Status = ThreadStatus.New,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };

        var initialMessage = new ThreadMessage
        {
            Body = message,
            IsFromAdmin = false,
            SentAt = DateTime.UtcNow,
            Source = MessageSource.ContactForm
        };

        thread.Messages.Add(initialMessage);

        context.Threads.Add(thread);
        await context.SaveChangesAsync();

        return thread;
    }

    public async Task<List<ContactThread>> GetAllThreadsAsync()
    {
        await using var context = await _factory.CreateDbContextAsync();

        return await context.Threads
            .Include(t => t.Messages)
            .OrderByDescending(t => t.LastActivityAt)
            .ToListAsync();
    }

    public async Task<ContactThread?> GetThreadByIdAsync(int id)
    {
        await using var context = await _factory.CreateDbContextAsync();

        return await context.Threads
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<(ThreadMessage Message, OutboxItem Outbox)> AddReplyAndQueueAsync(
        int threadId, string replyBody, string recipientEmail)
    {
        await using var context = await _factory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var thread = await context.Threads.FindAsync(threadId);
        if (thread == null)
        {
            throw new InvalidOperationException($"Tråd med id {threadId} hittades inte.");
        }

        var messageId = GenerateMessageId();

        var reply = new ThreadMessage
        {
            ThreadId = threadId,
            Body = replyBody,
            IsFromAdmin = true,
            SentAt = DateTime.UtcNow,
            Source = MessageSource.AdminReply,
            MessageId = messageId
        };

        thread.LastActivityAt = DateTime.UtcNow;
        thread.Status = ThreadStatus.Replied;

        context.Messages.Add(reply);
        await context.SaveChangesAsync();

        // Samma transaktion - hämta det sparade meddelandet
        var savedMessage = await context.Messages.FindAsync(reply.Id);
        if (savedMessage == null || string.IsNullOrWhiteSpace(savedMessage.MessageId))
        {
            throw new InvalidOperationException("Meddelandet kunde inte sparas korrekt.");
        }

        var outboxItem = new OutboxItem
        {
            ThreadMessageId = savedMessage.Id,
            RecipientEmail = recipientEmail,
            MessageId = savedMessage.MessageId,
            Status = OutboxItemStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        context.OutboxItems.Add(outboxItem);
        await context.SaveChangesAsync();

        // Explicit commit av transaktionen
        await transaction.CommitAsync();

        return (savedMessage, outboxItem);
    }


    public async Task<List<OutboxItem>> GetPendingOutboxItemsAsync(int maxResults = 100)
    {
        await using var context = await _factory.CreateDbContextAsync();

        var now = DateTime.UtcNow;
        return await context.OutboxItems
            .Where(o => o.Status == OutboxItemStatus.Pending &&
                        (o.NextRetryAt == null || o.NextRetryAt <= now))
            .OrderBy(o => o.CreatedAt)
            .Take(maxResults)
            .ToListAsync();
    }

    public async Task UpdateOutboxItemStatusAsync(int outboxItemId, OutboxItemStatus status, string reservationToken, string? failureReason = null)
    {
        await using var context = await _factory.CreateDbContextAsync();

        var item = await context.OutboxItems.FindAsync(outboxItemId);
        if (item == null)
        {
            throw new InvalidOperationException($"Outbox-post med id {outboxItemId} hittades inte.");
        }

        // Validera att anroparen äger reservationen
        if (!string.Equals(item.ReservationToken, reservationToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Reservation token mismatch för outbox-post {outboxItemId}.");
        }

        const int maxAttempts = 5;

        item.Status = status;
        item.UpdatedAt = DateTime.UtcNow;

        if (status == OutboxItemStatus.Sent)
        {
            item.SentAt = DateTime.UtcNow;
            item.ProcessingBy = null;
            item.ProcessingStartedAt = null;
            item.ReservationToken = null;
        }
        else if (status == OutboxItemStatus.Failed)
        {
            item.FailureReason = failureReason;
            item.AttemptCount++;

            // Om maximalt antal försök har nåtts, sätt status till Uncertain för manuell granskning
            if (item.AttemptCount >= maxAttempts)
            {
                item.Status = OutboxItemStatus.Uncertain;
                item.ProcessingBy = null;
                item.ProcessingStartedAt = null;
                item.ReservationToken = null;
            }
            else
            {
                // Exponentiell backoff: 5 min, 15 min, 1 h, 4 h, etc.
                var delayMinutes = (int)Math.Min(Math.Pow(3, item.AttemptCount) + 5, 1440);
                item.NextRetryAt = DateTime.UtcNow.AddMinutes(delayMinutes);
                item.ProcessingBy = null;
                item.ProcessingStartedAt = null;
                item.ReservationToken = null;
            }
        }

        await context.SaveChangesAsync();
    }

    private static string GenerateMessageId()
    {
        // RFC 5322-kompatibel Message-ID: <timestamp.guid@domain>
        return $"<{DateTime.UtcNow:yyyyMMddHHmmss}.{Guid.NewGuid().ToString("N")}@ramii.dev>";
    }
    // Bakåtkompatibilitet - slösa inte resurser på denna metod i Fas 1
    public async Task<ThreadMessage> AddReplyAsync(int threadId, string replyBody, bool isFromAdmin)
    {
        await using var context = await _factory.CreateDbContextAsync();

        var thread = await context.Threads.FindAsync(threadId);
        if (thread == null)
        {
            throw new InvalidOperationException($"Tråd med id {threadId} hittades inte.");
        }

        var messageId = GenerateMessageId();

        var reply = new ThreadMessage
        {
            ThreadId = threadId,
            Body = replyBody,
            IsFromAdmin = isFromAdmin,
            SentAt = DateTime.UtcNow,
            Source = isFromAdmin ? MessageSource.AdminReply : MessageSource.ContactForm,
            MessageId = messageId
        };

        thread.LastActivityAt = DateTime.UtcNow;
        thread.Status = isFromAdmin ? ThreadStatus.Replied : ThreadStatus.Open;

        context.Messages.Add(reply);
        await context.SaveChangesAsync();

        return reply;
    }

    public async Task<OutboxItem> QueueReplyForDeliveryAsync(int threadMessageId, string recipientEmail)
    {
        await using var context = await _factory.CreateDbContextAsync();

        var message = await context.Messages
            .FirstOrDefaultAsync(m => m.Id == threadMessageId);

        if (message == null)
        {
            throw new InvalidOperationException($"Meddelande med id {threadMessageId} hittades inte.");
        }

        if (string.IsNullOrWhiteSpace(message.MessageId))
        {
            throw new InvalidOperationException("Meddelandet saknar Message-ID.");
        }

        var outboxItem = new OutboxItem
        {
            ThreadMessageId = threadMessageId,
            RecipientEmail = recipientEmail,
            MessageId = message.MessageId,
            Status = OutboxItemStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        context.OutboxItems.Add(outboxItem);
        await context.SaveChangesAsync();

        return outboxItem;
    }

    // Fas 2: Atomär reservation för SMTP-worker-bearbetning
    // Förhindrar att två workers griper samma outbox-post samtidigt med ExecuteUpdateAsync
    public async Task<OutboxItem?> ReserveForProcessingAsync(string processingBy, int maxLockDurationSeconds = 300)
    {
        await using var context = await _factory.CreateDbContextAsync();

        var now = DateTime.UtcNow;
        var lockTimeout = now.AddSeconds(-maxLockDurationSeconds);
        var reservationToken = Guid.NewGuid().ToString();

        // Atomär UPDATE: Hitta och låsa en post i samma databasoperation
        // ExecuteUpdateAsync ser till att ingen annan worker kan gripa samma post mellan SELECT och UPDATE
        var affectedCount = await context.OutboxItems
            .Where(o => o.Status == OutboxItemStatus.Pending &&
                        (o.NextRetryAt == null || o.NextRetryAt <= now) &&
                        (o.ProcessingBy == null || o.ProcessingStartedAt < lockTimeout))
            .OrderBy(o => o.CreatedAt)
            .Take(1)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.ProcessingBy, processingBy)
                .SetProperty(o => o.ProcessingStartedAt, now)
                .SetProperty(o => o.ReservationToken, reservationToken));

        // Endast en reservation får lyckas
        if (affectedCount == 0)
        {
            return null;
        }

        // Hämta den reserverade posten för att returnera den
        var item = await context.OutboxItems
            .Where(o => o.ProcessingBy == processingBy && o.ReservationToken == reservationToken)
            .FirstOrDefaultAsync();

        return item;
    }

    // Fas 2: Rensa reservation vid timeout eller fel - kräver giltigt token
    // Utgångna reservationer klassificeras som Uncertain för manuell granskning
    public async Task ReleaseProcessingReservationAsync(int outboxItemId, string reservationToken)
    {
        await using var context = await _factory.CreateDbContextAsync();

        var item = await context.OutboxItems.FindAsync(outboxItemId);
        if (item == null)
        {
            throw new InvalidOperationException($"Outbox-post med id {outboxItemId} hittades inte.");
        }

        // Validera token
        if (!string.Equals(item.ReservationToken, reservationToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Reservation token mismatch för outbox-post {outboxItemId}.");
        }

        // Klassa som Uncertain för manuell behandling (kan inte veta om föregående worker skickade)
        item.Status = OutboxItemStatus.Uncertain;
        item.ProcessingBy = null;
        item.ProcessingStartedAt = null;
        item.ReservationToken = null;
        item.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }
}
