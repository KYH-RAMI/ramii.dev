using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;
using ramii.dev.Core.Interfaces;
using ramii.dev.Security;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.RateLimiting;
namespace ramii.dev.Services;

public static class ContactEndpoint
{
    private const int MaxRequestBytes = 32 * 1024;
    internal const int MinNameLength = 3;

    internal const string EmailPattern = @"[^@\s]+@(?:[A-Za-z0-9](?:[A-Za-z0-9\-]*[A-Za-z0-9])?\.)+[A-Za-z][A-Za-z0-9\-]*[A-Za-z0-9]";

    public static IServiceCollection AddContactProtection(this IServiceCollection services)
    {
        services.AddSingleton<ContactCooldown>();
        services.AddAntiforgery();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                ContactLimit(GetNetworkKey, permitLimit: 3, TimeSpan.FromMinutes(10)),
                ContactLimit(_ => "contact", permitLimit: 30, TimeSpan.FromMinutes(10)));

            options.AddPolicy("login", context =>
            RateLimitPartition.GetSlidingWindowLimiter(
                GetNetworkKey(context), _ => new()
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0
                }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                            Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                var message = context.HttpContext.Request.Path
                .StartsWithSegments("/api/auth/login")
             ? "För många inloggningsförsök. Vänta en stund och försök igen."
             : "För många kontaktförsök. Vänta en stund och försök igen.";

                await context.HttpContext.Response.WriteAsync(
                    message, cancellationToken);
            };
        });

        return services;
    }

    public static void MapContactForm(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost("/api/contact", SubmitAsync).WithMetadata(new ContactFormEndpoint());

    private static PartitionedRateLimiter<HttpContext> ContactLimit(
        Func<HttpContext, string> key, int permitLimit, TimeSpan window) =>
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<ContactFormEndpoint>() is null)
            {
                return RateLimitPartition.GetNoLimiter("other");
            }

            return RateLimitPartition.GetSlidingWindowLimiter(key(context), _ => new()
            {
                PermitLimit = permitLimit,
                Window = window,
                SegmentsPerWindow = 10,
                QueueLimit = 0
            });
        });

    private static string GetNetworkKey(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address?.IsIPv4MappedToIPv6 == true)
        {
            address = address.MapToIPv4();
        }

        return address?.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? Convert.ToHexString(address.GetAddressBytes().AsSpan(0, 8))
            : address?.ToString() ?? "unknown";
    }
    private static async Task<IResult> SubmitAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IContactService contactService,
        ContactCooldown cooldown)
    {
        var request = context.Request;
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType) ||
            !string.Equals(contentType.MediaType.Value, "application/x-www-form-urlencoded",
                StringComparison.OrdinalIgnoreCase))
        {
            return Results.Text("Använd kontaktformuläret på startsidan.", statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        if (request.ContentLength > MaxRequestBytes)
        {
            return Results.Text("Meddelandet är för stort.", statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } size &&
            (size.MaxRequestBodySize is null || size.MaxRequestBodySize > MaxRequestBytes))
        {
            size.MaxRequestBodySize = MaxRequestBytes;
        }

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(context.RequestAborted);
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (BadHttpRequestException exception)
        {
            return Results.Text("Kontaktförsöket kunde inte läsas.", statusCode: exception.StatusCode);
        }
        catch (InvalidDataException)
        {
            return Results.Text("Kontaktförsöket kunde inte läsas.", statusCode: StatusCodes.Status400BadRequest);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.LocalRedirect("/?kontakt=expired#kontakt");
        }

        if (!string.IsNullOrWhiteSpace(form["website"]))
        {
            return Results.LocalRedirect("/?kontakt=sent#kontakt");
        }

        var submission = new ContactSubmission(
            form["name"].ToString().Trim(),
            form["email"].ToString().Trim(),
            form["subject"].ToString().Trim(),
            form["message"].ToString().Trim());

        if (!Validator.TryValidateObject(submission, new ValidationContext(submission), null, true))
        {
            return Results.LocalRedirect("/?kontakt=invalid#kontakt");
        }

        var networkKey = GetNetworkKey(context);

        if (!cooldown.TryBegin(networkKey, out var retryAt))
        {
            if (retryAt is { } deadline)
            {
                SetCooldownCookie(context, deadline, "wait");

                return Results.LocalRedirect(
                    "/?kontakt=cooldown#kontakt");
            }

            return Results.Text(
                "Ett kontaktförsök behandlas redan.",
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        try
        {
            await contactService.CreateThreadAsync(
                submission.Name,
                submission.Email,
                submission.Subject,
                submission.Message);

            var until = cooldown.Complete(networkKey);

            SetCooldownCookie(context, until, "sent");

            return Results.LocalRedirect(
                "/?kontakt=sent#kontakt");
        }
        finally
        {
            cooldown.Cancel(networkKey);
        }

    }
    private static void SetCooldownCookie(
    HttpContext context,
    DateTimeOffset until,
    string status)
    {
        context.Response.Cookies.Append(
            "ramii_contact_cooldown",
            $"{status}:{until.ToUnixTimeMilliseconds()}",
            new CookieOptions
            {
                Secure = true,
                HttpOnly = false,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                Expires = until,
                Path = "/"
            });
    }

    private sealed class ContactFormEndpoint { }

    private sealed record ContactSubmission(
        [property: Required, StringLength(100, MinimumLength = MinNameLength)] string Name,
        [property: Required, EmailAddress, StringLength(150), RegularExpression(EmailPattern)] string Email,
        [property: Required, StringLength(150)] string Subject,
        [property: Required, StringLength(2000)] string Message);
}

