namespace NServiceBus.Transport.SQS;

using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Logging;

static class Renewal
{
    // This method does not check whether the visibility time has expired. That is because it's possible to renew the visibility even when the visibility time has expired.
    // The receipt handle remains valid until another consumer or a receiving attempt picks up the message.
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
                if (Logger.IsDebugEnabled)
                {
                    Logger.DebugFormat(
                        "The message visibility timeout for message with native ID '{0}' expiring at '{1}' UTC will be renewed after '{2}'.",
                        receivedMessage.MessageId, visibilityExpiresOn, renewAfter);
                }

                // We're awaiting the task created by 'ContinueWith' to avoid awaiting the Delay task which may be canceled.
                // This way we prevent a TaskCanceledException. We do this here because we expect this cancellation scenario
                // to be rather frequent and it avoids the cost of throwing/catching an exception.
                Task delayTask = await Task.Delay(renewAfter, timeProvider, cancellationToken)
                    .ContinueWith(
                        t => t,
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default)
                    .ConfigureAwait(false);

                if (delayTask.IsCanceled)
                {
                    LogStopped(receivedMessage, visibilityExpiresOn);
                    return Result.Stopped;
                }

                var utcNow = timeProvider.GetUtcNow();
                var remainingTime = visibilityExpiresOn - utcNow;
                // It is possible that the remaining time is negative when for example the continuation task scheduling takes too long
                // In those cases we attempt to overextend the visibility timeout by the positive remaining time in seconds
                var calculatedVisibilityTimeout =
                    Math.Max(Math.Abs((int)remainingTime.TotalSeconds) + visibilityTimeoutInSeconds,
                        visibilityTimeoutInSeconds);
                // immediately calculating the new expiry before updating the visibility to be on the safe side
                // since we can't make any assumptions on how long the call to ChangeMessageVisibilityAsync will take
                // It is OK for this to be cancellable because we don't want to attempt to renew the visibility timeout
                // when the message processing is already done which would trigger the token.
                visibilityExpiresOn = utcNow.Add(TimeSpan.FromSeconds(calculatedVisibilityTimeout));
                await sqsClient.ChangeMessageVisibilityAsync(
                    new ChangeMessageVisibilityRequest
                    {
                        QueueUrl = inputQueueUrl,
                        ReceiptHandle = receivedMessage.ReceiptHandle,
                        VisibilityTimeout = calculatedVisibilityTimeout
                    },
                    cancellationToken).ConfigureAwait(false);

                if (Logger.IsDebugEnabled)
                {
                    Logger.DebugFormat(
                        "Renewed message visibility timeout by '{0}' seconds until '{1}' UTC for message with native ID '{2}'.",
                        calculatedVisibilityTimeout, visibilityExpiresOn, receivedMessage.MessageId);
                }
            }
            catch (ReceiptHandleIsInvalidException ex)
            {
                if (Logger.IsWarnEnabled)
                {
                    Logger.Warn(
                        $"Renewing the message visibility timeout failed because the receipt handle was not valid for message with native ID '{receivedMessage.MessageId}'.",
                        ex);
                }

                // Signaling the message receipt handle is invalid so that other operations relaying on the token owned
                // by this token source can be cancelled.
                await messageVisibilityLostCancellationTokenSource.CancelAsync()
                    .ConfigureAwait(false);
                return Result.Failed;
            }
            catch (AmazonSQSException ex) when (ex.IsCausedByMessageVisibilityExpiry())
            {
                // This is debug level because we expect this to happen when the message is picked up by a competing consumer
                // or when the message successfully completed processing and the message is deleted
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug(
                        $"Renewing the message visibility timeout failed because the message with native ID {receivedMessage.MessageId} has already been picked up by a competing consumer.",
                        ex);
                }

                // Signaling the message receipt handle is invalid so that other operations relaying on the token owned
                // by this token source can be cancelled.
                await messageVisibilityLostCancellationTokenSource.CancelAsync()
                    .ConfigureAwait(false);
                return Result.Failed;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                LogStopped(receivedMessage, visibilityExpiresOn);
                return Result.Stopped;
            }
            catch (Exception ex)
            {
                if (Logger.IsWarnEnabled)
                {
                    Logger.Warn($"Renewing the message visibility timeout failed for the message with native ID '{receivedMessage.MessageId}'.", ex);
                }

                return Result.Failed;
            }
        }

        return Result.Stopped;
    }

    static void LogStopped(Message receivedMessage, DateTimeOffset visibilityExpiresOn)
    {
        if (Logger.IsDebugEnabled)
        {
            Logger.DebugFormat("The message visibility timeout renewal for message with native ID '{0}' was stopped. The message visibility might expire at '{1}' UTC", receivedMessage.MessageId, visibilityExpiresOn);
        }
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

        // For any renewal below 400ms we want the renewal to be immediate
        // Why 400ms? Glad you asked. It is an arbitrary number chosen to  factor in some buffer
        // for the time it takes to actually do the renewal calling the SQS API with the network latency
        if (remainingTime < TimeSpan.FromMilliseconds(400))
        {
            return TimeSpan.Zero;
        }

        var buffer = TimeSpan.FromTicks(Math.Min(remainingTime.Ticks / 2, MaximumRenewBufferDuration.Ticks));
        var renewAfter = remainingTime - buffer;

        return renewAfter;
    }

    static readonly TimeSpan MaximumRenewBufferDuration = TimeSpan.FromSeconds(10);

    static readonly ILog Logger = LogManager.GetLogger(typeof(Renewal));
}