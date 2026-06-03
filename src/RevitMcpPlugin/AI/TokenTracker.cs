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

        public static int SessionInputTokens => _sessionInputTokens;
        public static int SessionOutputTokens => _sessionOutputTokens;
        public static int SessionCachedInputTokens => _sessionCachedInputTokens;
        public static int SessionCacheCreationInputTokens => _sessionCacheCreationInputTokens;
        public static int SessionCallCount => _sessionCallCount;

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
        }

        public static void RestoreSessionUsage(int inputTokens, int outputTokens, bool incrementCallCount = false)
        {
            Interlocked.Add(ref _sessionInputTokens, inputTokens);
            Interlocked.Add(ref _sessionOutputTokens, outputTokens);
            if (incrementCallCount) Interlocked.Increment(ref _sessionCallCount);
        }
    }
}
