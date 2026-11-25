using ramii.dev.Core.Entities;

namespace ramii.dev.Services;

public sealed record BackstageAdminSummary(
    string Username, string Email, DateTime CreatedAt, DateTime? LastLoginAt);

public interface IBackstageOperations
{
    Task<List<ContactThread>> GetThreadsAsync();
    Task ReplyAsync(int threadId, string body);
    Task<List<BackstageAdminSummary>> GetAdminsAsync();
    Task<bool> CreateAdminAsync(string username, string email, string password);
}