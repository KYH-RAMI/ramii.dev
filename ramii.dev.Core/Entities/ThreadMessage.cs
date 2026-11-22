namespace ramii.dev.Core.Entities;

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
}
