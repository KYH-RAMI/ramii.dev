using Microsoft.Extensions.DependencyInjection;
using ramii.dev.Services;

namespace ramii.dev.Security;

public static class BackstagePolicy
{
    public const string Name = "BackstageAdmin";
    public const string Role = "Admin";

    public static IServiceCollection AddBackstage(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(Name, policy =>
                policy.RequireAuthenticatedUser().RequireRole(Role));
        });
        services.AddScoped<IBackstageOperations, BackstageOperations>();
        return services;
    }
}