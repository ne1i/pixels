using System.Collections.Concurrent;

namespace pixels_site.Api.Services;

public class RateLimiter
{
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _requestLog = new();
    private readonly int _maxRequests;
    private readonly TimeSpan _timeWindow;

    public RateLimiter(int maxRequests = 10, int timeWindowSeconds = 1)
    {
        _maxRequests = maxRequests;
        _timeWindow = TimeSpan.FromSeconds(timeWindowSeconds);
    }

    public bool IsAllowed(string identifier)
    {
        var now = DateTime.UtcNow;
        var queue = _requestLog.GetOrAdd(identifier, _ => new Queue<DateTime>());

        lock (queue)
        {
            // Remove old entries outside the time window
            while (queue.Count > 0 && now - queue.Peek() > _timeWindow)
            {
                queue.Dequeue();
            }

            // Check if limit exceeded
            if (queue.Count >= _maxRequests)
            {
                return false;
            }

            // Add current request
            queue.Enqueue(now);
            return true;
        }
    }

    public void Cleanup()
    {
        var now = DateTime.UtcNow;
        foreach (var key in _requestLog.Keys)
        {
            if (_requestLog.TryGetValue(key, out var queue))
            {
                lock (queue)
                {
                    if (queue.Count == 0 || now - queue.Peek() > _timeWindow.Add(TimeSpan.FromMinutes(1)))
                    {
                        _requestLog.TryRemove(key, out _);
                    }
                }
            }
        }
    }
}
