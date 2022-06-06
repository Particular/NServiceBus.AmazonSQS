namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Threading.Tasks;

    //implementation adapted from https://david-desmaisons.github.io/RateLimiter/
    //we couldn't use the OSS package due to dependencies constrains: the above-linked package requires .NET 4.7.2
    abstract partial class RateLimiter
    {
        protected RateLimiter(int maxAllowedRequests, TimeSpan timeConstraint, string limitedApiName) => awaitableConstraint = new AwaitableConstraint(maxAllowedRequests, timeConstraint, limitedApiName);

#pragma warning disable PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
#pragma warning disable PS0013 // A Func used as a method parameter with a Task, ValueTask, or ValueTask<T> return type argument should have at least one CancellationToken parameter type argument unless it has a parameter type argument implementing ICancellableContext
        public async Task<T> Execute<T>(Func<Task<T>> taskToExecute)
#pragma warning restore PS0013 // A Func used as a method parameter with a Task, ValueTask, or ValueTask<T> return type argument should have at least one CancellationToken parameter type argument unless it has a parameter type argument implementing ICancellableContext
#pragma warning restore PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        {
            using (await awaitableConstraint.WaitIfNeeded().ConfigureAwait(false))
            {
                return await taskToExecute().ConfigureAwait(false);
            }
        }

        readonly AwaitableConstraint awaitableConstraint;
    }
}