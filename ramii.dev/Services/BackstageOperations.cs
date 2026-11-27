using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using ramii.dev.Core.Entities;
using ramii.dev.Core.Interfaces;
using ramii.dev.Data;
using ramii.dev.Security;

namespace ramii.dev.Services;

public sealed class BackstageOperations : IBackstageOperations, IDisposable
{
    private readonly AuthenticationStateProvider authenticationStateProvider;
    private readonly IAuthorizationService authorizationService;
    private readonly IContactService contactService;
    private readonly IAuthService authService;
    private readonly IDbContextFactory<AppDbContext> dbContextFactory;
    private volatile Task<AuthenticationState>? updatedAuthenticationState;

    public BackstageOperations(
        AuthenticationStateProvider authenticationStateProvider,
        IAuthorizationService authorizationService,
        IContactService contactService,
        IAuthService authService,
        IDbContextFactory<AppDbContext> dbContextFactory)
    {
        this.authenticationStateProvider = authenticationStateProvider;
        this.authorizationService = authorizationService;
        this.contactService = contactService;
        this.authService = authService;
        this.dbContextFactory = dbContextFactory;
        authenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> state) =>
        updatedAuthenticationState = state;

    public void Dispose() =>
        authenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;

    public async Task<List<ContactThread>> GetThreadsAsync()
    {
        await RequireAdminAsync();
        return await contactService.GetAllThreadsAsync();
    }

    public async Task<BackstageThreadDetail?> GetThreadDetailAsync(int threadId)
    {
        await RequireAdminAsync();

        await using var context = await dbContextFactory.CreateDbContextAsync();

        var thread = await context.Threads
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == threadId);

        if (thread == null)
        {
            return null;
        }

        // Hämta leveransstatus för varje meddelande
        var messages = new List<BackstageMessage>();
        foreach (var msg in thread.Messages.OrderBy(m => m.SentAt))
        {
            var outbox = await context.OutboxItems
                .FirstOrDefaultAsync(o => o.ThreadMessageId == msg.Id);

            messages.Add(new BackstageMessage(
                msg.Id,
                msg.Body,
                msg.IsFromAdmin,
                msg.SentAt,
                msg.Source,
                msg.MessageId,
                outbox?.Status));
        }

        // Hämta alla utboxmeddelanden för denna tråd
        var outboxItems = await context.OutboxItems
            .Include(o => o.ThreadMessage)
            .Where(o => o.ThreadMessage!.ThreadId == threadId)
            .ToListAsync();

        return new BackstageThreadDetail(
            thread.Id,
            thread.VisitorName,
            thread.VisitorEmail,
            thread.Subject,
            thread.Status,
            thread.CreatedAt,
            thread.LastActivityAt,
            messages,
            outboxItems);
    }

    public async Task ReplyAsync(int threadId, string body)
    {
        await RequireAdminAsync();

        // Hämta tråden för att få besökarens e-postadress
        var thread = await contactService.GetThreadByIdAsync(threadId);
        if (thread == null)
        {
            throw new InvalidOperationException($"Tråd med id {threadId} hittades inte.");
        }

        // Spara svar och köa för e-postutskick atomärt
        await contactService.AddReplyAndQueueAsync(threadId, body, thread.VisitorEmail);
    }

    public async Task<List<BackstageAdminSummary>> GetAdminsAsync()
    {
        await RequireAdminAsync();
        var admins = await authService.GetAllAdminsAsync();
        return admins.Select(admin => new BackstageAdminSummary(
            admin.Username, admin.Email, admin.CreatedAt, admin.LastLoginAt)).ToList();
    }

    public async Task<bool> CreateAdminAsync(string username, string email, string password)
    {
        await RequireAdminAsync();
        return await authService.CreateAdminAsync(username, email, password);
    }

    private async Task RequireAdminAsync()
    {
        var state = await (updatedAuthenticationState ?? authenticationStateProvider.GetAuthenticationStateAsync());
        var result = await authorizationService.AuthorizeAsync(
            state.User, resource: null, policyName: BackstagePolicy.Name);

        if (!result.Succeeded)
        {
            throw new UnauthorizedAccessException("Åtgärden kräver administratörsbehörighet.");
        }
    }
}
