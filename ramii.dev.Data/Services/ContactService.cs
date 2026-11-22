using Microsoft.EntityFrameworkCore;
using ramii.dev.Core.Entities;
using ramii.dev.Core.Interfaces;

namespace ramii.dev.Data.Services;

public class ContactService : IContactService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ContactService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<ContactThread> CreateThreadAsync(string name, string email, string subject, string message)
    {
        await using var context = await _factory.CreateDbContextAsync();

        var thread = new ContactThread
        {
            VisitorName = name,
            VisitorEmail = email,
            Subject = subject,
            Status = ThreadStatus.New,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };

        var initialMessage = new ThreadMessage
        {
            Body = message,
            IsFromAdmin = false,
            SentAt = DateTime.UtcNow
        };

        thread.Messages.Add(initialMessage);

        context.Threads.Add(thread);
        await context.SaveChangesAsync();

        return thread;
    }

    public async Task<List<ContactThread>> GetAllThreadsAsync()
    {
        await using var context = await _factory.CreateDbContextAsync();

        return await context.Threads
            .Include(t => t.Messages)
            .OrderByDescending(t => t.LastActivityAt)
            .ToListAsync();
    }

    public async Task<ContactThread?> GetThreadByIdAsync(int id)
    {
        await using var context = await _factory.CreateDbContextAsync();

        return await context.Threads
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<ThreadMessage> AddReplyAsync(int threadId, string replyBody, bool isFromAdmin)
    {
        await using var context = await _factory.CreateDbContextAsync();

        var thread = await context.Threads.FindAsync(threadId);
        if (thread == null)
        {
            throw new InvalidOperationException($"Tråd med id {threadId} hittades inte.");
        }

        var reply = new ThreadMessage
        {
            ThreadId = threadId,
            Body = replyBody,
            IsFromAdmin = isFromAdmin,
            SentAt = DateTime.UtcNow
        };

        thread.LastActivityAt = DateTime.UtcNow;
        thread.Status = isFromAdmin ? ThreadStatus.Replied : ThreadStatus.Open;

        context.Messages.Add(reply);
        await context.SaveChangesAsync();

        return reply;
    }
}
