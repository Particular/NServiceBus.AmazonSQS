namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    class RateLimiter
    {
        class AwaitableConstraint
        {
            public AwaitableConstraint(int maxAllowedRequests, TimeSpan timeConstraint)
            {
                if (maxAllowedRequests <= 0)
                {
                    throw new ArgumentException($"{nameof(maxAllowedRequests)} must be greater than 0.", nameof(maxAllowedRequests));
                }

                if (timeConstraint.TotalMilliseconds <= 0)
                {
                    throw new ArgumentException($"{nameof(timeConstraint)} must be greater than 0.", nameof(timeConstraint));
                }

                this.maxAllowedRequests = maxAllowedRequests;
                this.timeConstraint = timeConstraint;
                requestsTimeStamps = new SizeConstrainedStack<DateTime>(this.maxAllowedRequests);
            }

            public async Task<IDisposable> WaitIfNeeded()
            {
                await semaphore.WaitAsync().ConfigureAwait(false);

                var requestsCount = 0;
                var now = DateTime.Now;
                var allocatedTimeLowerBound = now - timeConstraint;
                var request = requestsTimeStamps.First;
                LinkedListNode<DateTime> lastRequest = null;
                while ((request != null) && (request.Value > allocatedTimeLowerBound))
                {
                    //counting how many requests have already
                    //been performed within the allocated time
                    lastRequest = request;
                    request = request.Next;
                    requestsCount++;
                }

                if (requestsCount < maxAllowedRequests)
                {
                    return new DisposableAction(OnActionDisposed);
                }

                Debug.Assert(request == null);
                Debug.Assert(lastRequest != null);
                var timeToWait = lastRequest.Value.Add(timeConstraint) - now;
                try
                {
                    await Task.Delay(timeToWait).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    _ = semaphore.Release();
                    throw;
                }

                return new DisposableAction(OnActionDisposed);
            }

            void OnActionDisposed()
            {
                //pushing as time stamp the request completion time
                requestsTimeStamps.Push(DateTime.Now);
                _ = semaphore.Release();
            }

            readonly SizeConstrainedStack<DateTime> requestsTimeStamps;

            readonly int maxAllowedRequests;
            TimeSpan timeConstraint;
            readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        }

        class SizeConstrainedStack<T> : LinkedList<T>
        {
            public SizeConstrainedStack(int maxSize)
            {
                this.maxSize = maxSize;
            }

            public void Push(T item)
            {
                AddFirst(item);

                if (Count > maxSize)
                {
                    RemoveLast();
                }
            }

            readonly int maxSize;
        }

        class DisposableAction : IDisposable
        {
            public DisposableAction(Action onDisposedCallback)
            {
                this.onDisposedCallback = onDisposedCallback;
            }

            public void Dispose()
            {
                onDisposedCallback?.Invoke();
                onDisposedCallback = null;
            }

            Action onDisposedCallback;
        }

        public RateLimiter(int maxAllowedRequests, TimeSpan timeConstraint) => awaitableConstraint = new AwaitableConstraint(maxAllowedRequests, timeConstraint);

        public async Task<T> Execute<T>(Func<Task<T>> taskToExecute)
        {
            using (await awaitableConstraint.WaitIfNeeded().ConfigureAwait(false))
            {
                return await taskToExecute().ConfigureAwait(false);
            }
        }

        readonly AwaitableConstraint awaitableConstraint;
    }
}