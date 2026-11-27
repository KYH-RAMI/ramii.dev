namespace ramii.dev.Security;

internal sealed class ContactCooldown
{
    private static readonly TimeSpan Duration =
        TimeSpan.FromMinutes(2);

    private readonly object _sync = new();

    private readonly Dictionary<string, DateTimeOffset>
        _cooldowns = new();

    private readonly HashSet<string> _inFlight = new();

    public bool TryBegin(
        string key,
        out DateTimeOffset? retryAt)
    {
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;

            foreach (var expired in _cooldowns
                .Where(entry => entry.Value <= now)
                .Select(entry => entry.Key)
                .ToArray())
            {
                _cooldowns.Remove(expired);
            }

            retryAt = null;

            if (_cooldowns.TryGetValue(key, out var until))
            {
                retryAt = until;
                return false;
            }

            return _inFlight.Add(key);
        }
    }

    public DateTimeOffset Complete(string key)
    {
        lock (_sync)
        {
            var until = DateTimeOffset.UtcNow.Add(Duration);

            _cooldowns[key] = until;
            _inFlight.Remove(key);

            return until;
        }
    }

    public void Cancel(string key)
    {
        lock (_sync)
        {
            _inFlight.Remove(key);
        }
    }
}