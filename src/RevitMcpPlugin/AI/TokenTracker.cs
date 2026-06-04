using System.Threading;

namespace RevitMcpPlugin.AI
{
    public static class TokenTracker
    {
        private static int _sessionInputTokens;
        private static int _sessionOutputTokens;
        private static int _sessionCachedInputTokens;
        private static int _sessionCacheCreationInputTokens;
        private static int _sessionCallCount;

        private static readonly object _turnUsagesLock = new();
        private static readonly List<TokenUsage> _turnUsages = new();
        private static TokenUsage? _lastTurnUsage;

        public static int SessionInputTokens => _sessionInputTokens;
        public static int SessionOutputTokens => _sessionOutputTokens;
        public static int SessionCachedInputTokens => _sessionCachedInputTokens;
        public static int SessionCacheCreationInputTokens => _sessionCacheCreationInputTokens;
        public static int SessionCallCount => _sessionCallCount;

        public static IReadOnlyList<TokenUsage> TurnUsages
        {
            get
            {
                lock (_turnUsagesLock) return _turnUsages.ToArray();
            }
        }

        public static TokenUsage? LastTurnUsage
        {
            get
            {
                lock (_turnUsagesLock) return _lastTurnUsage;
            }
        }

        public static double SessionCacheHitRatio
        {
            get
            {
                int total = _sessionInputTokens + _sessionCachedInputTokens;
                return total == 0 ? 0.0 : (double)_sessionCachedInputTokens / total;
            }
        }

        public static void Track(string callType, string provider, string model,
            int inputTokens, int outputTokens, string? requestId = null,
            int cachedInputTokens = 0, int cacheCreationInputTokens = 0)
        {
            Interlocked.Add(ref _sessionInputTokens, inputTokens);
            Interlocked.Add(ref _sessionOutputTokens, outputTokens);
            Interlocked.Add(ref _sessionCachedInputTokens, cachedInputTokens);
            Interlocked.Add(ref _sessionCacheCreationInputTokens, cacheCreationInputTokens);
            Interlocked.Increment(ref _sessionCallCount);

            var usage = new TokenUsage
            {
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                CachedInputTokens = cachedInputTokens,
                CacheCreationInputTokens = cacheCreationInputTokens,
                Timestamp = DateTime.UtcNow
            };
            lock (_turnUsagesLock)
            {
                _turnUsages.Add(usage);
                _lastTurnUsage = usage;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[TokenTracker] rid={requestId} type={callType} in={inputTokens} out={outputTokens} " +
                $"cache_read={cachedInputTokens} cache_create={cacheCreationInputTokens} " +
                $"session_total_in={SessionInputTokens} session_total_out={SessionOutputTokens} " +
                $"session_cache_read={SessionCachedInputTokens} hit_ratio={SessionCacheHitRatio:P1}");
        }

        public static void ResetSession()
        {
            Interlocked.Exchange(ref _sessionInputTokens, 0);
            Interlocked.Exchange(ref _sessionOutputTokens, 0);
            Interlocked.Exchange(ref _sessionCachedInputTokens, 0);
            Interlocked.Exchange(ref _sessionCacheCreationInputTokens, 0);
            Interlocked.Exchange(ref _sessionCallCount, 0);
            lock (_turnUsagesLock)
            {
                _turnUsages.Clear();
                _lastTurnUsage = null;
            }
        }

        public static void RestoreSessionUsage(int inputTokens, int outputTokens, bool incrementCallCount = false)
        {
            Interlocked.Add(ref _sessionInputTokens, inputTokens);
            Interlocked.Add(ref _sessionOutputTokens, outputTokens);
            if (incrementCallCount) Interlocked.Increment(ref _sessionCallCount);
        }
    }

    public sealed class TokenUsage
    {
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int CachedInputTokens { get; set; }
        public int CacheCreationInputTokens { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
