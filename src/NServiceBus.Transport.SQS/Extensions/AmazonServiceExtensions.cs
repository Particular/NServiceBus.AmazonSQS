namespace NServiceBus.Transport.SQS.Extensions
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.Runtime;

    static class AmazonServiceExtensions
    {
        public static async Task<T> RetryConflictsAsync<T>(this IAmazonService client, Func<CancellationToken, Task<T>> a, Action<int> onRetry, CancellationToken cancellationToken = default)
        {
            _ = client;

            var tryCount = 0;
            var sleepTimeMs = 2000;
            const int maxTryCount = 5;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    tryCount++;
                    return await a(cancellationToken).ConfigureAwait(false);
                }
                catch (AmazonServiceException ex)
                    when (ex.StatusCode == HttpStatusCode.Conflict &&
                          ex.ErrorCode == "OperationAborted")
                {
                    if (tryCount >= maxTryCount)
                    {
                        throw;
                    }

                    var sleepTime = sleepTimeMs * tryCount;
                    onRetry(sleepTime);
                    await Task.Delay(sleepTime, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}