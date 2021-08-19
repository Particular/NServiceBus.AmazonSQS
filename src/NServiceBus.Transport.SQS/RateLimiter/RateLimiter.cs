namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Threading.Tasks;

    //implementation adapted from https://david-desmaisons.github.io/RateLimiter/
    //we couldn't use the OSS package due to dependencies constrains: the above-linked package requires .NET 4.7.2
    abstract partial class RateLimiter
    {
        protected RateLimiter(int maxAllowedRequests, TimeSpan timeConstraint, string limitedApiName) => awaitableConstraint = new AwaitableConstraint(maxAllowedRequests, timeConstraint, limitedApiName);

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