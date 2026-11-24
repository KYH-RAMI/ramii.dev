using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ramii.dev.Components;
using ramii.dev.Data;
using ramii.dev.Core.Interfaces;
using ramii.dev.Data.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Registrera databasen (EF Core SQL Server)
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddScoped<IContactService, ContactService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.Cookie.Name = "ramii_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Inloggnings-endpoint
app.MapPost("/api/auth/login", async (
    HttpContext httpContext,
    IAuthService authService) =>
{
    var form = await httpContext.Request.ReadFormAsync();
    var username = form["username"].ToString().Trim();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
    {
        return Results.Redirect("/login?error=invalid");
    }

    var isValid = await authService.ValidateCredentialsAsync(username, password);
    if (!isValid)
    {
        return Results.Redirect("/login?error=invalid");
    }

    // Uppdatera inloggningstidpunkten i databasen
    await authService.UpdateLastLoginAsync(username);

    var claims = new List<Claim>
    {
        new(ClaimTypes.Name, username),
        new(ClaimTypes.Role, "Admin")
    };

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var authProperties = new AuthenticationProperties
    {
        IsPersistent = true,
        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
    };

    await httpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity),
        authProperties);

    var target = !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/')
        ? returnUrl
        : "/backstage";

    return Results.Redirect(target);
}).DisableAntiforgery();


// Utloggnings-endpoint
app.MapGet("/api/auth/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
if (args.Contains("--create-admin"))
{
    using var scope = app.Services.CreateScope();
    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

    Console.WriteLine("\n[>] ramii.dev - Skapa ny administratör");
    Console.Write("Användarnamn: ");
    var username = Console.ReadLine()?.Trim() ?? string.Empty;

    Console.Write("E-post: ");
    var email = Console.ReadLine()?.Trim() ?? string.Empty;

    Console.Write("Lösenord: ");
    var password = ReadPassword();

    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        Console.WriteLine("[!] Alla fält är obligatoriska.");
        return;
    }

    var success = await authService.CreateAdminAsync(username, email, password);
    Console.WriteLine(success
        ? "[+] Administratör skapad framgångsrikt!"
        : "[-] Fel: Användarnamnet eller e-postadressen finns redan.");

    return; // Avslutar direkt utan att starta webbservern
}

app.Run();
static string ReadPassword()
{
    var pass = string.Empty;
    ConsoleKey key;
    do
    {
        var keyInfo = Console.ReadKey(intercept: true);
        key = keyInfo.Key;

        if (key == ConsoleKey.Backspace && pass.Length > 0)
        {
            pass = pass[0..^1];
            Console.Write("\b \b");
        }
        else if (!char.IsControl(keyInfo.KeyChar))
        {
            pass += keyInfo.KeyChar;
            Console.Write("*");
        }
    } while (key != ConsoleKey.Enter);
    Console.WriteLine();
    return pass;
}
