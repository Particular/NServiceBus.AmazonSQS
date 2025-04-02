namespace NServiceBus.Transport.SQS;

using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;

static class Renewal
{
    // This method does not check whether the visibility time has already expired. The reason being that it is possible to renew the visibility
    // even when the visibility time has expired as long as the message has not been picked up by another consumer or receive attempt the
    // original receipt handle is still valid.
    public static async Task<Result> RenewMessageVisibility(Message receivedMessage, DateTimeOffset visibilityExpiresOn,
        int visibilityTimeoutInSeconds, IAmazonSQS sqsClient, string inputQueueUrl,
        CancellationTokenSource messageVisibilityLostCancellationTokenSource, TimeProvider timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        timeProvider ??= TimeProvider.System;
        while (!cancellationToken.IsCancellationRequested)
        {
            var renewAfter = CalculateRenewalTime(visibilityExpiresOn, timeProvider);
            try
            {
                // We're awaiting the task created by 'ContinueWith' to avoid awaiting the Delay task which may be canceled.
                // This way we prevent a TaskCanceledException.
                Task delayTask = await Task.Delay(renewAfter, timeProvider, cancellationToken)
                    .ContinueWith(
                        t => t,
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default)
                    .ConfigureAwait(false);

                if (delayTask.IsCanceled)
                {
                    return Result.Stopped;
                }

                var utcNow = timeProvider.GetUtcNow();
                var remainingTime = visibilityExpiresOn - utcNow;
                // It is possible that the remaining time is negative when for example the continuation task scheduling takes too long
                // In those cases we attempt to overextend the visibility timeout by the positive remaining time in seconds
                var calculatedVisibilityTimeout =
                    Math.Max(Math.Abs((int)remainingTime.TotalSeconds) + visibilityTimeoutInSeconds,
                        visibilityTimeoutInSeconds);
                // immediately calculating the new expiry before calling updating the visibility to be on the safe side
                // since we can't make any assumptions on how long the call to ChangeMessageVisibilityAsync will take
                // we don't want this to be cancellable because we are doing best-effort to complete inflight messages
                // on shutdown.
                visibilityExpiresOn = utcNow.Add(TimeSpan.FromSeconds(calculatedVisibilityTimeout));
                await sqsClient.ChangeMessageVisibilityAsync(
                    new ChangeMessageVisibilityRequest
                    {
                        QueueUrl = inputQueueUrl,
                        ReceiptHandle = receivedMessage.ReceiptHandle,
                        VisibilityTimeout = calculatedVisibilityTimeout
                    },
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (ReceiptHandleIsInvalidException)
            {
                // Signaling the message receipt handle is invalid so that other operations relaying on the token owned
                // by this token source can be cancelled.
                await messageVisibilityLostCancellationTokenSource.CancelAsync()
                    .ConfigureAwait(false);
                return Result.Failed;
            }
#pragma warning disable PS0019
            catch (Exception)
#pragma warning restore PS0019
            {
                return Result.Failed;
            }
        }

        return Result.Stopped;
    }

    public enum Result
    {
        Failed,
        Stopped
    }

    public static TimeSpan CalculateRenewalTime(DateTimeOffset visibilityTimeExpiresOn,
        TimeProvider timeProvider = null)
    {
        timeProvider ??= TimeProvider.System;
        var remainingTime = visibilityTimeExpiresOn - timeProvider.GetUtcNow();

        if (remainingTime < TimeSpan.FromMilliseconds(400))
        {
            return TimeSpan.Zero;
        }

        var buffer = TimeSpan.FromTicks(Math.Min(remainingTime.Ticks / 2, MaximumRenewBufferDuration.Ticks));
        var renewAfter = remainingTime - buffer;

        return renewAfter;
    }

    static readonly TimeSpan MaximumRenewBufferDuration = TimeSpan.FromSeconds(10);
}