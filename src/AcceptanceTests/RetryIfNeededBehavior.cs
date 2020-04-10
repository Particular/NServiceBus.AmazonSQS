namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using Logging;
    using NServiceBus.Pipeline;

    class RetryIfNeededBehavior : Behavior<IOutgoingLogicalMessageContext>
    {
        public override async Task Invoke(IOutgoingLogicalMessageContext context, Func<Task> next)
        {
            var iterationCount = 0;
            TopicHasNoPermissionToAccessTheDestinationException exception;
            do
            {
                try
                {
                    iterationCount++;
                    await next().ConfigureAwait(false);
                    exception = null;
                }
                catch (TopicHasNoPermissionToAccessTheDestinationException ex)
                {
                    var millisecondsDelay = iterationCount * 1000;
                    Logger.Debug($"Destination '{ex.EndpointArn}' was not reachable from topic '{ex.TopicArn}'! Retrying in {millisecondsDelay} ms.");
                    exception = ex;
                    await Task.Delay(millisecondsDelay).ConfigureAwait(false);
                }
            } while (exception != null && iterationCount < 11);
        }

        static ILog Logger = LogManager.GetLogger<RetryIfNeededBehavior>();
    }
}