namespace ramii.dev.Core.Entities;

public enum MessageSource
{
    ContactForm = 0,
    EmailIncoming = 1,
    AdminReply = 2
}

public class ThreadMessage
{
    public int Id { get; set; }

    public int ThreadId { get; set; }
    public ContactThread? Thread { get; set; }

    public string Body { get; set; } = string.Empty;
    public bool IsFromAdmin { get; set; } = false;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    // E-postreferenser för korrelation och deduplicering
    public string? MessageId { get; set; }
    public string? InReplyToMessageId { get; set; }
    public string? References { get; set; }

    // Meddelandekälla för att skilja mellan formulär, e-post och svar
    public MessageSource Source { get; set; } = MessageSource.ContactForm;
}
