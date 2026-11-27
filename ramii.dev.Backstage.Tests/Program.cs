using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ramii.dev.Core.Entities;
using ramii.dev.Core.Interfaces;
using ramii.dev.Data;
using ramii.dev.Security;
using ramii.dev.Services;

var services = new ServiceCollection();
services.AddLogging();

// Registrera test AppDbContext-factory (in-memory database)
services.AddDbContextFactory<AppDbContext>(options =>
    options.UseInMemoryDatabase("test-db"));

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
// ReplyAsync anropar nu: GetThreadByIdAsync (1) + AddReplyAndQueueAsync (1) = 2 nya anrop, plus GetThreadsAsync (1) = 3 totalt
Check(contacts.Calls == 3 && admins.Calls == 2, "alla operationer når lagringen");
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
        return Task.FromResult<List<ContactThread>>([new ContactThread { Id = 42, VisitorEmail = "visitor@example.test" }]);
    }

    public Task<ThreadMessage> AddReplyAsync(int threadId, string replyBody, bool isFromAdmin)
    {
        Calls++;
        Reply = (threadId, replyBody, isFromAdmin);
        return Task.FromResult(new ThreadMessage { Id = 100, MessageId = "<test@ramii.dev>" });
    }

    public Task<ContactThread?> GetThreadByIdAsync(int id)
    {
        Calls++;
        return Task.FromResult<ContactThread?>(new ContactThread { Id = 42, VisitorEmail = "visitor@example.test" });
    }

    public Task<ContactThread> CreateThreadAsync(string name, string email, string subject, string message) =>
        throw new NotSupportedException();

    public async Task<(ThreadMessage Message, OutboxItem Outbox)> AddReplyAndQueueAsync(int threadId, string replyBody, string recipientEmail)
    {
        Calls++;
        Reply = (threadId, replyBody, true);
        var message = new ThreadMessage { Id = 100, MessageId = "<test@ramii.dev>" };
        var outbox = new OutboxItem { Id = 1, Status = OutboxItemStatus.Pending, ReservationToken = null };
        return (message, outbox);
    }

    public Task UpdateOutboxItemStatusAsync(int outboxItemId, OutboxItemStatus status, string reservationToken, string? failureReason = null) =>
        Task.CompletedTask;

    public Task<OutboxItem?> ReserveForProcessingAsync(string processingBy, int maxLockDurationSeconds = 300)
    {
        var outbox = new OutboxItem { Id = 1, Status = OutboxItemStatus.Pending, ReservationToken = Guid.NewGuid().ToString() };
        return Task.FromResult<OutboxItem?>(outbox);
    }

    public Task ReleaseProcessingReservationAsync(int outboxItemId, string reservationToken) =>
        Task.CompletedTask;
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