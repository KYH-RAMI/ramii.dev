using ramii.dev.Core.Entities;

namespace ramii.dev.Core.Interfaces;

public interface IContactService
{
    // Skapa en ny tråd från kontaktformuläret
    Task<ContactThread> CreateThreadAsync(string name, string email, string subject, string message);

    // Hämta alla trådar till Backstage-inkorgen
    Task<List<ContactThread>> GetAllThreadsAsync();

    // Hämta en specifik tråd med alla dess meddelanden
    Task<ContactThread?> GetThreadByIdAsync(int id);

    // Skicka svar från Backstage
    Task<ThreadMessage> AddReplyAsync(int threadId, string replyBody, bool isFromAdmin);
}
