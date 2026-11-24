using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ramii.dev.Core.Entities;
using ramii.dev.Core.Interfaces;

namespace ramii.dev.Data.Services;

public class AuthService : IAuthService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    // DevSecOps-standard för PBKDF2
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 350_000;

    public AuthService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<bool> ValidateCredentialsAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return false;

        await using var context = await _contextFactory.CreateDbContextAsync();
        var user = await context.AdminUsers
            .FirstOrDefaultAsync(u => u.Username == username.Trim());

        if (user == null || string.IsNullOrWhiteSpace(user.PasswordHash))
            return false;

        return VerifyPassword(password, user.PasswordHash);
    }

    public async Task UpdateLastLoginAsync(string username)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var user = await context.AdminUsers
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.Trim().ToLower());

        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task EnsureAdminCreatedAsync(string username, string initialPassword)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var exists = await context.AdminUsers.AnyAsync(u => u.Username.ToLower() == username.Trim().ToLower());

        if (!exists)
        {
            var admin = new AdminUser
            {
                Username = username.Trim().ToLower(),
                PasswordHash = HashPassword(initialPassword),
                CreatedAt = DateTime.UtcNow
            };

            context.AdminUsers.Add(admin);
            await context.SaveChangesAsync();
        }
    }

    // Kryptografisk hashning med salt (PBKDF2-SHA256)
    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2)
            return false;

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
    public async Task<bool> CreateAdminAsync(string username, string email, string password)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var exists = await context.AdminUsers.AnyAsync(u => u.Username == username || u.Email == email);
        if (exists) return false;

        var admin = new AdminUser
        {
            Username = username.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = HashPassword(password),
            CreatedAt = DateTime.UtcNow
        };

        context.AdminUsers.Add(admin);
        await context.SaveChangesAsync();
        return true;
    }
    public async Task<List<AdminUser>> GetAllAdminsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AdminUsers
            .OrderByDescending(u => u.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }


}
