using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using ramii.dev.Components;
using ramii.dev.Core.Configuration;
using ramii.dev.Core.Interfaces;
using ramii.dev.Data;
using ramii.dev.Data.Services;
using ramii.dev.Security;
using ramii.dev.Services;
using System.Security.Claims;


var builder = WebApplication.CreateBuilder(args);

// Läs Docker Secrets i produktionsmiljön
if (builder.Environment.IsProduction())
{
    builder.Configuration.AddKeyPerFile(
        directoryPath: "/run/secrets",
        optional: true);
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Registrera databasen (EF Core SQL Server)
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Registrera SMTP- och IMAP-inställningar
builder.Services.Configure<SmtpSettings>(
    builder.Configuration.GetSection("Email:Smtp"));

builder.Services.Configure<ImapSettings>(
    builder.Configuration.GetSection("Email:Imap"));

// Hämta autentiseringsuppgifter från konfiguration
builder.Services.PostConfigure<SmtpSettings>(options =>
{
    options.Username = builder.Configuration["Email:Username"] ?? string.Empty;
    options.Password = builder.Configuration["Email:Password"] ?? string.Empty;
});

builder.Services.PostConfigure<ImapSettings>(options =>
{
    options.Username = builder.Configuration["Email:Username"] ?? string.Empty;
    options.Password = builder.Configuration["Email:Password"] ?? string.Empty;
});

builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ImapService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/access-denied";
        options.Cookie.Name = "ramii_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.SlidingExpiration = false;
    });

builder.Services.AddBackstage();
builder.Services.AddContactProtection();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHostedService<EmailOutboxWorker>();
builder.Services.AddHostedService<ImapSyncWorker>();

var app = builder.Build();

if (args.Contains("--migrate"))
{
    var contextFactory = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var context = await contextFactory.CreateDbContextAsync();

    Console.WriteLine("Tillämpar väntande databasmigreringar...");
    await context.Database.MigrateAsync();
    Console.WriteLine("Databasen är uppdaterad.");
    return;
}

app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (path.StartsWithSegments("/backstage") || path.StartsWithSegments("/api") ||
        path.Equals("/login") || path.Equals("/access-denied"))
    {
        context.Response.Headers["X-Robots-Tag"] = "noindex";
    }

    await next(context);
});


// Inloggnings-endpoint
app.MapPost("/api/auth/login", async (
    HttpContext httpContext,
    IAuthService authService,
    IAntiforgery antiforgery) =>
{
    // Validera antiforgery-token
    try
    {
        await antiforgery.ValidateRequestAsync(httpContext);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest("Ogiltig säkerhetstoken.");
    }

    var form = await httpContext.Request.ReadFormAsync(
        httpContext.RequestAborted);

    var username = form["username"].ToString().Trim();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    if (string.IsNullOrWhiteSpace(username) ||
        string.IsNullOrWhiteSpace(password))
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
        new(ClaimTypes.Role, BackstagePolicy.Role)
    };

    var identity = new ClaimsIdentity(
        claims, CookieAuthenticationDefaults.AuthenticationScheme);

    var authProperties = new AuthenticationProperties
    {
        IsPersistent = false
    };

    await httpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity),
        authProperties);

    var target = RedirectHttpResult.IsLocalUrl(returnUrl)
        ? returnUrl
        : "/backstage";

    return Results.LocalRedirect(target);
})
.RequireRateLimiting("login");


// Utloggnings-endpoint
app.MapPost("/api/auth/logout", async (
    HttpContext httpContext,
    IAntiforgery antiforgery) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(httpContext);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest("Ogiltig säkerhetstoken.");
    }

    await httpContext.SignOutAsync(
        CookieAuthenticationDefaults.AuthenticationScheme);

    return Results.LocalRedirect("/login");
})
.RequireAuthorization();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute(
    "/not-found",
    createScopeForStatusCodePages: true);

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();
app.UseAntiforgery();

app.MapContactForm();
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
