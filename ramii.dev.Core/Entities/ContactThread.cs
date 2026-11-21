namespace ramii.dev.Core.Entities;

public enum ThreadStatus
{
    New = 0,
    Open = 1,
    Replied = 2,
    Closed = 3
}

public class ContactThread
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string VisitorName { get; set; } = string.Empty;
    public string VisitorEmail { get; set; } = string.Empty;
    public ThreadStatus Status { get; set; } = ThreadStatus.New;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    // Navigation property: alla inlägg i tråden
    public List<ThreadMessage> Messages { get; set; } = new();
}
