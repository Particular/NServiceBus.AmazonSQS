#nullable enable
namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    // Dedicated Lazy wrapper that is not general purpose but written for the caching use cases in
    // the topic and subscription cache
    sealed class AsyncCacheLazy<T> : Lazy<Task<T>>
    {
        // Uses Task.Run here to dispatch the value factory on the worker thread pool to make sure the value factory
        // is never run under the context of the thread accessing .Value since the caches are used in the dispatcher
        // which could be used in an environment with a UI thread. Given topics and subscriptions have a cache ttl
        // and we have a finite number of items to cache double dispatching for safety reasons should be OK and is
        // hopefully better than trying to pretend the value factory might not capture context.
#pragma warning disable PS0013
        public AsyncCacheLazy(Func<Task<T>> valueFactory) : base(() => Task.Run(() => valueFactory()), LazyThreadSafetyMode.ExecutionAndPublication)
#pragma warning restore PS0013
        {
        }

        public new bool IsValueCreated => base.IsValueCreated && Value is { IsCompleted: true };

        public TaskAwaiter<T> GetAwaiter() => Value.GetAwaiter();

        public ConfiguredTaskAwaitable<T> ConfigureAwait(bool continueOnCapturedContext)
            => Value.ConfigureAwait(continueOnCapturedContext);
    }
}