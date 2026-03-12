using System.Collections.Concurrent;

namespace SpoolManager.Server.Services;

public class LoginRateLimiter
{
    private readonly ConcurrentDictionary<string, (int Count, DateTime BlockedUntil)> _attempts = new();
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public bool IsBlocked(string key)
    {
        if (!_attempts.TryGetValue(key, out var entry)) return false;
        if (entry.BlockedUntil > DateTime.UtcNow) return true;
        if (entry.BlockedUntil != default && entry.BlockedUntil <= DateTime.UtcNow)
        {
            _attempts.TryRemove(key, out _);
        }
        return false;
    }

    public TimeSpan? GetRemainingLockout(string key)
    {
        if (!_attempts.TryGetValue(key, out var entry)) return null;
        if (entry.BlockedUntil > DateTime.UtcNow)
            return entry.BlockedUntil - DateTime.UtcNow;
        return null;
    }

    public void RecordFailure(string key)
    {
        _attempts.AddOrUpdate(key,
            _ => (1, default),
            (_, existing) =>
            {
                var newCount = existing.Count + 1;
                if (newCount >= MaxAttempts)
                    return (newCount, DateTime.UtcNow.Add(LockoutDuration));
                return (newCount, existing.BlockedUntil);
            });
    }

    public void RecordSuccess(string key)
    {
        _attempts.TryRemove(key, out _);
    }
}
