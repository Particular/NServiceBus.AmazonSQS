namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    //implementation adapted from https://david-desmaisons.github.io/RateLimiter/
    //we couldn't use the OSS package due to dependencies constrains: the above-linked package requires .NET 4.7.2
    abstract partial class RateLimiter
    {
        protected RateLimiter(int maxAllowedRequests, TimeSpan timeConstraint, string limitedApiName) => awaitableConstraint = new AwaitableConstraint(maxAllowedRequests, timeConstraint, limitedApiName);

        public async Task<T> Execute<TArgs, T>(Func<TArgs, CancellationToken, Task<T>> taskToExecute, TArgs args, CancellationToken cancellationToken = default)
        {
            using (await awaitableConstraint.WaitIfNeeded(cancellationToken).ConfigureAwait(false))
            {
                return await taskToExecute(args, cancellationToken).ConfigureAwait(false);
            }
        }

        readonly AwaitableConstraint awaitableConstraint;
    }
}