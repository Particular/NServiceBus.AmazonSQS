namespace NServiceBus.Transport.SQS
{
    using System;

    partial class RateLimiter
    {
        class DisposableAction : IDisposable
        {
            public DisposableAction(Action onDisposedCallback)
            {
                this.onDisposedCallback = onDisposedCallback;
            }

            public void Dispose()
            {
                onDisposedCallback?.Invoke();
                onDisposedCallback = null;
            }

            Action onDisposedCallback;
        }
    }
}