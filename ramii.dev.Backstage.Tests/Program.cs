using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using ramii.dev.Core.Entities;
using ramii.dev.Core.Interfaces;
using ramii.dev.Security;
using ramii.dev.Services;

var services = new ServiceCollection();
services.AddLogging();
services.AddScoped<TestAuthenticationState>();
services.AddScoped<AuthenticationStateProvider>(provider =>
    provider.GetRequiredService<TestAuthenticationState>());
services.AddScoped<ContactStore>();
services.AddScoped<IContactService>(provider => provider.GetRequiredService<ContactStore>());
services.AddScoped<AdminStore>();
services.AddScoped<IAuthService>(provider => provider.GetRequiredService<AdminStore>());
services.AddBackstage();

using var provider = services.BuildServiceProvider(validateScopes: true);
using var scope = provider.CreateScope();
var backstage = scope.ServiceProvider.GetRequiredService<IBackstageOperations>();
var identity = scope.ServiceProvider.GetRequiredService<TestAuthenticationState>();
var contacts = scope.ServiceProvider.GetRequiredService<ContactStore>();
var admins = scope.ServiceProvider.GetRequiredService<AdminStore>();

await VerifyDeniedAsync("anonym");

identity.SetUser(new ClaimsPrincipal(new ClaimsIdentity(
    [new Claim(ClaimTypes.Role, "Reader")], "Test")));
await VerifyDeniedAsync("inloggad utan Admin");

identity.SetUser(new ClaimsPrincipal(new ClaimsIdentity(
    [new Claim(ClaimTypes.Role, BackstagePolicy.Role)], "Test")));
Check((await backstage.GetThreadsAsync()).Single().Id == 42, "inkorgens resultat");
await backstage.ReplyAsync(42, "Svar");
Check(contacts.Reply == (42, "Svar", true), "svaret sparas som adminsvar");
Check((await backstage.GetAdminsAsync()).Single().Username == "admin", "adminlistans resultat");
Check(await backstage.CreateAdminAsync("nyadmin", "ny@example.test", "test-password"),
    "resultat från adminskapande");
Check(contacts.Calls == 2 && admins.Calls == 2, "alla fyra operationer når lagringen");
Console.WriteLine("OK: Admin får utföra alla fyra operationer.");

identity.SetUser(new ClaimsPrincipal(new ClaimsIdentity()));
await VerifyDeniedAsync("samma DI-scope efter att testidentiteten blivit anonym");

async Task VerifyDeniedAsync(string label)
{
    var calls = (contacts.Calls, admins.Calls);
    await ExpectDeniedAsync(() => backstage.GetThreadsAsync(), "inkorg");
    await ExpectDeniedAsync(() => backstage.ReplyAsync(42, "Svar"), "adminsvar");
    await ExpectDeniedAsync(() => backstage.GetAdminsAsync(), "adminlista");
    await ExpectDeniedAsync(() => backstage.CreateAdminAsync(
        "nyadmin", "ny@example.test", "test-password"), "adminskapande");
    Check((contacts.Calls, admins.Calls) == calls, $"ingen lagring för {label}");
    Console.WriteLine($"OK: {label} nekas alla fyra operationer före lagring.");
}

static async Task ExpectDeniedAsync(Func<Task> operation, string name)
{
    try
    {
        await operation();
    }
    catch (UnauthorizedAccessException)
    {
        return;
    }

    throw new InvalidOperationException($"Förväntade nekad åtkomst för {name}.");
}

static void Check(bool condition, string description)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Testet misslyckades: {description}.");
    }
}

sealed class TestAuthenticationState : AuthenticationStateProvider
{
    private ClaimsPrincipal user = new(new ClaimsIdentity());

    public void SetUser(ClaimsPrincipal principal)
    {
        user = principal;
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(new AuthenticationState(user));
}

sealed class ContactStore : IContactService
{
    public int Calls { get; private set; }
    public (int ThreadId, string Body, bool IsFromAdmin)? Reply { get; private set; }

    public Task<List<ContactThread>> GetAllThreadsAsync()
    {
        Calls++;
        return Task.FromResult<List<ContactThread>>([new ContactThread { Id = 42 }]);
    }

    public Task<ThreadMessage> AddReplyAsync(int threadId, string replyBody, bool isFromAdmin)
    {
        Calls++;
        Reply = (threadId, replyBody, isFromAdmin);
        return Task.FromResult(new ThreadMessage());
    }

    public Task<ContactThread> CreateThreadAsync(string name, string email, string subject, string message) =>
        throw new NotSupportedException();

    public Task<ContactThread?> GetThreadByIdAsync(int id) => throw new NotSupportedException();
}

sealed class AdminStore : IAuthService
{
    public int Calls { get; private set; }

    public Task<List<AdminUser>> GetAllAdminsAsync()
    {
        Calls++;
        return Task.FromResult<List<AdminUser>>([new AdminUser { Username = "admin" }]);
    }

    public Task<bool> CreateAdminAsync(string username, string email, string password)
    {
        Calls++;
        return Task.FromResult(true);
    }

    public Task<bool> ValidateCredentialsAsync(string username, string password) =>
        throw new NotSupportedException();

    public Task UpdateLastLoginAsync(string username) => throw new NotSupportedException();

    public Task EnsureAdminCreatedAsync(string username, string initialPassword) =>
        throw new NotSupportedException();
}