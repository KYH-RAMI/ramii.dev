using ramii.dev.Core.Entities;

namespace ramii.dev.Core.Interfaces;

public interface IAuthService
{
    Task<bool> ValidateCredentialsAsync(string username, string password);
    Task UpdateLastLoginAsync(string username);
    Task EnsureAdminCreatedAsync(string username, string initialPassword);

    Task<bool> CreateAdminAsync(string username, string email, string password);
    Task<List<AdminUser>> GetAllAdminsAsync();

}

