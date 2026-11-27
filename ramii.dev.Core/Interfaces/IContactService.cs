using ramii.dev.Core.Entities;

namespace ramii.dev.Core.Interfaces;

public interface IContactService
{
    // Skapa en ny tråd från kontaktformuläret
    Task<ContactThread> CreateThreadAsync(string name, string email, string subject, string message);

    // Hämta alla trådar till Backstage-inkorgen
    Task<List<ContactThread>> GetAllThreadsAsync();

    // Hämta en specifik tråd med alla dess meddelanden
    Task<ContactThread?> GetThreadByIdAsync(int id);

    // Skicka svar från Backstage och köa det för e-postutskick
    Task<ThreadMessage> AddReplyAsync(int threadId, string replyBody, bool isFromAdmin);

    // Skicka svar från Backstage och köa det för e-postutskick atomärt (ny metod)
    Task<(ThreadMessage Message, OutboxItem Outbox)> AddReplyAndQueueAsync(int threadId, string replyBody, string recipientEmail);

    // Fas 2: Atomär reservation för worker-bearbetning - förhindrar dubbletter
    // Returnerar OutboxItem med en unik ReservationToken om reservation lyckades, null annars
    Task<OutboxItem?> ReserveForProcessingAsync(string processingBy, int maxLockDurationSeconds = 300);

    // Fas 2: Markera utgående e-post som sent/misslyckat med reservationstoken-validering
    Task UpdateOutboxItemStatusAsync(int outboxItemId, OutboxItemStatus status, string reservationToken, string? failureReason = null);

    // Fas 2: Rensa reservation vid timeout eller fel - kräver giltigt token
    Task ReleaseProcessingReservationAsync(int outboxItemId, string reservationToken);
}

