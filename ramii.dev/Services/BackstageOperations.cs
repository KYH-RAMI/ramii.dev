using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using ramii.dev.Core.Entities;
using ramii.dev.Core.Interfaces;
using ramii.dev.Security;

namespace ramii.dev.Services;

public sealed class BackstageOperations : IBackstageOperations, IDisposable
{
    private readonly AuthenticationStateProvider authenticationStateProvider;
    private readonly IAuthorizationService authorizationService;
    private readonly IContactService contactService;
    private readonly IAuthService authService;
    private volatile Task<AuthenticationState>? updatedAuthenticationState;

    public BackstageOperations(
        AuthenticationStateProvider authenticationStateProvider,
        IAuthorizationService authorizationService,
        IContactService contactService,
        IAuthService authService)
    {
        this.authenticationStateProvider = authenticationStateProvider;
        this.authorizationService = authorizationService;
        this.contactService = contactService;
        this.authService = authService;
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

    public async Task ReplyAsync(int threadId, string body)
    {
        await RequireAdminAsync();
        await contactService.AddReplyAsync(threadId, body, isFromAdmin: true);
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