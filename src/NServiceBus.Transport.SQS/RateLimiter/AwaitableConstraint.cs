namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;

    partial class RateLimiter
    {
        class AwaitableConstraint
        {
            public AwaitableConstraint(int maxAllowedRequests, TimeSpan timeConstraint, string apiName)
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
                this.apiName = apiName;
                requestsTimeStamps = new SizeConstrainedStack<DateTime>(this.maxAllowedRequests);
            }

            public async Task<DisposableAction> WaitIfNeeded(CancellationToken cancellationToken = default)
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                var requestsCount = 0;
                var now = DateTime.UtcNow;
                var allocatedTimeLowerBound = now - timeConstraint;
                var request = requestsTimeStamps.First;
                LinkedListNode<DateTime> lastRequest = null;
                while (request != null && request.Value > allocatedTimeLowerBound)
                {
                    //counting how many requests have already
                    //been performed within the allocated time
                    lastRequest = request;
                    request = request.Next;
                    requestsCount++;
                }

                if (requestsCount < maxAllowedRequests)
                {
                    return new DisposableAction(() => OnActionDisposed());
                }

                Debug.Assert(request == null);
                Debug.Assert(lastRequest != null);
                var timeToWait = lastRequest.Value.Add(timeConstraint) - now;
                try
                {
                    Logger.Info($"Requests threshold of {maxAllowedRequests} requests every {timeConstraint} reached for API '{apiName}'. Waiting {timeToWait}.");
                    await Task.Delay(timeToWait, cancellationToken).ConfigureAwait(false);
                }
#pragma warning disable PS0019
                catch (Exception)
#pragma warning restore PS0019
                {
                    _ = semaphore.Release();
                    throw;
                }

                return new DisposableAction(() => OnActionDisposed());
            }

            void OnActionDisposed()
            {
                //pushing as time stamp the request completion time
                requestsTimeStamps.Push(DateTime.UtcNow);
                _ = semaphore.Release();
            }

            readonly SizeConstrainedStack<DateTime> requestsTimeStamps;

            readonly int maxAllowedRequests;
            TimeSpan timeConstraint;
            readonly string apiName;
            readonly SemaphoreSlim semaphore = new(1, 1);
            static ILog Logger = LogManager.GetLogger(typeof(AwaitableConstraint));
        }
    }
}