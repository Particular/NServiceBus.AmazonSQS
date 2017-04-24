using Amazon.Runtime;
using System;
using System.Threading;

namespace NServiceBus.AmazonSQS
{
    static class AmazonServiceExtensions
    {
        public static T RetryConflicts<T>(this IAmazonService client, Func<T> a, Action<int> onRetry)
        {
            int tryCount = 0;
            int sleepTimeMs = 2000;
            const int maxTryCount = 5;
            
            while (true)
            {
                try
                {
                    tryCount++;
                    return a();
                }
                catch (AmazonServiceException ex)
                    when (ex.StatusCode == System.Net.HttpStatusCode.Conflict && 
                        ex.ErrorCode == "OperationAborted")
                {
                    if (tryCount >= maxTryCount)
                        throw;

                    var sleepTime = (sleepTimeMs * tryCount);
                    onRetry(sleepTime);
                    Thread.Sleep(sleepTime);
                }
            }
        }
    }
}
