namespace ramii.dev.Core.Interfaces;

public interface IEmailService
{
    Task<bool> SendOutboxItemAsync(int outboxItemId, string reservationToken);
}

