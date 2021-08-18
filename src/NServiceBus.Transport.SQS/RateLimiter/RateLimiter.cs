namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Threading.Tasks;

    partial class RateLimiter
    {
        public RateLimiter(int maxAllowedRequests, TimeSpan timeConstraint, string limitedApiName) => awaitableConstraint = new AwaitableConstraint(maxAllowedRequests, timeConstraint, limitedApiName);

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