using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;
using ramii.dev.Core.Interfaces;

namespace ramii.dev.Services;

public static class ContactEndpoint
{
    private const int MaxRequestBytes = 32 * 1024;

    internal const string EmailPattern = @"[^@\s]+@(?:[A-Za-z0-9](?:[A-Za-z0-9\-]*[A-Za-z0-9])?\.)+[A-Za-z][A-Za-z0-9\-]*[A-Za-z0-9]";

    public static IServiceCollection AddContactProtection(this IServiceCollection services)
    {
        services.AddAntiforgery();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                ContactLimit(GetNetworkKey, permitLimit: 3, TimeSpan.FromMinutes(10)),
                ContactLimit(_ => "contact", permitLimit: 30, TimeSpan.FromMinutes(10)));

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                await context.HttpContext.Response.WriteAsync(
                    "För många kontaktförsök. Vänta en stund och försök igen.", cancellationToken);
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
        HttpContext context, IAntiforgery antiforgery, IContactService contactService)
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

        await contactService.CreateThreadAsync(
            submission.Name, submission.Email, submission.Subject, submission.Message);
        return Results.LocalRedirect("/?kontakt=sent#kontakt");
    }

    private sealed class ContactFormEndpoint { }

    private sealed record ContactSubmission(
        [property: Required, StringLength(100)] string Name,
        [property: Required, EmailAddress, StringLength(150), RegularExpression(EmailPattern)] string Email,
        [property: Required, StringLength(150)] string Subject,
        [property: Required, StringLength(2000)] string Message);
}

