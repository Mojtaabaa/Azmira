using System.Collections.Concurrent;

namespace KonkoorHelp.Api.Services;

public sealed class AttemptLimiter(int limit, TimeSpan window)
{
    private readonly ConcurrentDictionary<string, HitEntry> _hits = new();

    public bool IsBlocked(string key)
    {
        if (!_hits.TryGetValue(key, out var entry)) return false;
        if (entry.ResetAt < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            _hits.TryRemove(key, out _);
            return false;
        }

        return entry.Count >= limit;
    }

    public void Hit(string key)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _hits.AddOrUpdate(
            key,
            _ => new HitEntry(1, now + (long)window.TotalMilliseconds),
            (_, entry) => entry.ResetAt < now
                ? new HitEntry(1, now + (long)window.TotalMilliseconds)
                : entry with { Count = entry.Count + 1 });
    }

    public void Clear(string key) => _hits.TryRemove(key, out _);

    private sealed record HitEntry(int Count, long ResetAt);
}
