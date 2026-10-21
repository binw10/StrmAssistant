using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StrmAssistant.Common;
using Xunit;

namespace StrmAssistant.Tests
{
    public class AdjustableSemaphoreTests
    {
        [Fact]
        public async Task Shrink_DrainsExistingHoldersBeforeAdmittingWaiter()
        {
            using var limiter = new AdjustableSemaphore(2);
            await limiter.WaitAsync();
            await limiter.WaitAsync();
            var waiting = limiter.WaitAsync();
            limiter.Resize(1);
            limiter.Release();
            Assert.False(waiting.IsCompleted);
            Assert.Equal(0, limiter.CurrentCount);
            limiter.Release();
            await waiting.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(0, limiter.CurrentCount);
            limiter.Release();
            Assert.Equal(1, limiter.CurrentCount);
        }

        [Fact]
        public async Task Grow_GrantsQueuedWaitersWithoutReplacingLimiter()
        {
            using var limiter = new AdjustableSemaphore(1);
            await limiter.WaitAsync();
            var first = limiter.WaitAsync();
            var second = limiter.WaitAsync();
            limiter.Resize(3);
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(0, limiter.CurrentCount);
            limiter.Release(); limiter.Release(); limiter.Release();
            Assert.Equal(3, limiter.CurrentCount);
        }

        [Fact]
        public async Task CancelledWaiter_DoesNotConsumeNextPermit()
        {
            using var limiter = new AdjustableSemaphore(1);
            await limiter.WaitAsync();
            using var cts = new CancellationTokenSource();
            var cancelled = limiter.WaitAsync(cts.Token);
            var next = limiter.WaitAsync();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
            limiter.Release();
            await next.WaitAsync(TimeSpan.FromSeconds(2));
            limiter.Release();
            Assert.Equal(1, limiter.CurrentCount);
        }

        [Fact]
        public async Task ConcurrentWorkers_StayWithinLimitWhileShrinking()
        {
            using var limiter = new AdjustableSemaphore(3);
            var active = 0;
            var peak = 0;
            var tasks = Enumerable.Range(0, 100).Select(async _ =>
            {
                await limiter.WaitAsync();
                try
                {
                    var count = Interlocked.Increment(ref active);
                    int observed;
                    do { observed = Volatile.Read(ref peak); }
                    while (count > observed && Interlocked.CompareExchange(ref peak, count, observed) != observed);
                    await Task.Delay(1);
                }
                finally { Interlocked.Decrement(ref active); limiter.Release(); }
            }).ToArray();
            limiter.Resize(1);
            await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.InRange(peak, 1, 3);
            Assert.Equal(0, active);
            Assert.Equal(1, limiter.CurrentCount);
        }

        [Fact]
        public void UnbalancedRelease_IsRejected()
        {
            using var limiter = new AdjustableSemaphore(1);
            Assert.Throws<SemaphoreFullException>(() => limiter.Release());
        }

        [Fact]
        public async Task Dispose_FaultsPendingWaiter()
        {
            var limiter = new AdjustableSemaphore(1);
            await limiter.WaitAsync();
            var waiting = limiter.WaitAsync();
            limiter.Dispose();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => waiting);
            limiter.Release();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void InvalidCapacity_IsRejected(int capacity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AdjustableSemaphore(capacity));
            using var limiter = new AdjustableSemaphore(1);
            Assert.Throws<ArgumentOutOfRangeException>(() => limiter.Resize(capacity));
            Assert.Equal(1, limiter.CurrentCount);
        }
    }
}
