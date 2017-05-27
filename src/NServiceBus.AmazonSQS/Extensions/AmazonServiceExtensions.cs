using Amazon.Runtime;
using System;
using System.Threading.Tasks;

namespace NServiceBus.AmazonSQS
{
    static class AmazonServiceExtensions
    {
        public static async Task<T> RetryConflictsAsync<T>(this IAmazonService client, Func<Task<T>> a, Action<int> onRetry)
        {
            int tryCount = 0;
            int sleepTimeMs = 2000;
            const int maxTryCount = 5;

            while (true)
            {
                try
                {
                    tryCount++;
                    return await a().ConfigureAwait(false);
                }
                catch (AmazonServiceException ex)
                    when (ex.StatusCode == System.Net.HttpStatusCode.Conflict &&
                        ex.ErrorCode == "OperationAborted")
                {
                    if (tryCount >= maxTryCount)
                        throw;

                    var sleepTime = (sleepTimeMs * tryCount);
                    onRetry(sleepTime);
                    await Task.Delay(sleepTime).ConfigureAwait(false);
                }
            }
        }
    }
}
