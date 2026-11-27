using ramii.dev.Core.Entities;

namespace ramii.dev.Services;

public sealed record BackstageAdminSummary(
    string Username, string Email, DateTime CreatedAt, DateTime? LastLoginAt);

public sealed record BackstageThreadDetail(
    int Id,
    string VisitorName,
    string VisitorEmail,
    string Subject,
    ThreadStatus Status,
    DateTime CreatedAt,
    DateTime LastActivityAt,
    List<BackstageMessage> Messages,
    List<OutboxItem> OutboxItems);

public sealed record BackstageMessage(
    int Id,
    string Body,
    bool IsFromAdmin,
    DateTime SentAt,
    MessageSource Source,
    string? MessageId,
    OutboxItemStatus? DeliveryStatus);

public interface IBackstageOperations
{
    Task<List<ContactThread>> GetThreadsAsync();
    Task<BackstageThreadDetail?> GetThreadDetailAsync(int threadId);
    Task ReplyAsync(int threadId, string body);
    Task<List<BackstageAdminSummary>> GetAdminsAsync();
    Task<bool> CreateAdminAsync(string username, string email, string password);
}