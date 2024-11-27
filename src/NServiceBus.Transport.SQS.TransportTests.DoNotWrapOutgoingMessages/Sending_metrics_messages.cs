namespace NServiceBus.TransportTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Transport;

    public class Sending_metrics_messages : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        public async Task Should_not_fail_when_using_do_not_wrap(
            TransportTransactionMode transactionMode)
        {
            var messageProcessed = CreateTaskCompletionSource<MessageContext>();

            await StartPump(
                (context, _) => messageProcessed.SetCompleted(context),
                (_, __) => Task.FromResult(ErrorHandleResult.Handled),
                TransportTransactionMode.None);

            var headers = new Dictionary<string, string>
            {
                { Transport.SQS.Constants.MetricsMessageMetricTypeHeaderKey, "doesn't matter" },
                { Headers.ContentType, Transport.SQS.Constants.MetricsMessageContentTypeHeaderValue }
            };
            var body = Guid.NewGuid().ToByteArray();

            await SendMessage(InputQueueName, headers, body: body);

            var messageContext = await messageProcessed.Task;

            Assert.That(messageContext.Headers, Is.Not.Empty);
            Assert.That(messageContext.Headers, Is.SupersetOf(headers));
        }
    }
}