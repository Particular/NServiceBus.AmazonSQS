namespace NServiceBus.AmazonSQS
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Amazon.Runtime;

    static class AmazonServiceExtensions
    {
        public static async Task<T> RetryConflictsAsync<T>(this IAmazonService client, Func<Task<T>> a, Action<int> onRetry)
        {
            var tryCount = 0;
            var sleepTimeMs = 2000;
            const int maxTryCount = 5;

            while (true)
            {
                try
                {
                    tryCount++;
                    return await a().ConfigureAwait(false);
                }
                catch (AmazonServiceException ex)
                    when (ex.StatusCode == HttpStatusCode.Conflict &&
                          ex.ErrorCode == "OperationAborted")
                {
                    if (tryCount >= maxTryCount)
                    {
                        throw;
                    }

                    var sleepTime = (sleepTimeMs * tryCount);
                    onRetry(sleepTime);
                    await Task.Delay(sleepTime).ConfigureAwait(false);
                }
            }
        }
    }
}