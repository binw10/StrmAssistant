using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.Common
{
    /// <summary>A stable limiter: lowering capacity drains existing holders before admitting new work.</summary>
    public sealed class AdjustableSemaphore : IDisposable
    {
        private readonly object _sync = new object();
        private readonly LinkedList<TaskCompletionSource<bool>> _waiters = new LinkedList<TaskCompletionSource<bool>>();
        private int _capacity;
        private int _holders;
        private bool _disposed;

        public AdjustableSemaphore(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        public int CurrentCount { get { lock (_sync) return Math.Max(0, _capacity - _holders); } }

        public Task WaitAsync(CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(AdjustableSemaphore));
                if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
                if (_holders < _capacity)
                {
                    _holders++;
                    return Task.CompletedTask;
                }
                var waiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var node = _waiters.AddLast(waiter);
                return WaitForPermit(node, cancellationToken);
            }
        }

        private async Task WaitForPermit(LinkedListNode<TaskCompletionSource<bool>> node, CancellationToken token)
        {
            using (token.Register(() =>
            {
                lock (_sync)
                {
                    if (node.List != null) _waiters.Remove(node);
                    node.Value.TrySetCanceled(token);
                }
            }))
                await node.Value.Task.ConfigureAwait(false);
        }

        public int Release()
        {
            lock (_sync)
            {
                if (_holders == 0) throw new SemaphoreFullException();
                var previous = Math.Max(0, _capacity - _holders);
                _holders--;
                DrainWaiters();
                return previous;
            }
        }

        public void Resize(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            lock (_sync)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(AdjustableSemaphore));
                _capacity = capacity;
                DrainWaiters();
            }
        }

        private void DrainWaiters()
        {
            while (!_disposed && _holders < _capacity && _waiters.Count > 0)
            {
                var waiter = _waiters.First.Value;
                _waiters.RemoveFirst();
                if (waiter.TrySetResult(true)) _holders++;
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _disposed = true;
                while (_waiters.Count > 0)
                {
                    var waiter = _waiters.First.Value;
                    _waiters.RemoveFirst();
                    waiter.TrySetException(new ObjectDisposedException(nameof(AdjustableSemaphore)));
                }
            }
        }
    }
}
