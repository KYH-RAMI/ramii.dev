namespace ramii.dev.Core.Entities;

public enum OutboxItemStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    Uncertain = 3
}

public class OutboxItem
{
    public int Id { get; set; }

    public int ThreadMessageId { get; set; }
    public ThreadMessage? ThreadMessage { get; set; }

    public string RecipientEmail { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;

    public OutboxItemStatus Status { get; set; } = OutboxItemStatus.Pending;

    public int AttemptCount { get; set; } = 0;
    public DateTime? NextRetryAt { get; set; }

    public string? FailureReason { get; set; }

    // Reservation för samtidig bearbetning - worker-ID under SMTP-försöket
    public string? ProcessingBy { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    // Unik token per reservationsförsök - för validering av reservationsägarskap
    public string? ReservationToken { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public DateTime? SentAt { get; set; }
}
