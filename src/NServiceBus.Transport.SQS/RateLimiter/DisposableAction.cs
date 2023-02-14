namespace NServiceBus.Transport.SQS
{
    using System;

    partial class RateLimiter
    {
        readonly struct DisposableAction : IDisposable
        {
            public DisposableAction(Action onDisposedCallback) => this.onDisposedCallback = onDisposedCallback;

            public void Dispose() => onDisposedCallback?.Invoke();

            readonly Action onDisposedCallback;
        }
    }
}