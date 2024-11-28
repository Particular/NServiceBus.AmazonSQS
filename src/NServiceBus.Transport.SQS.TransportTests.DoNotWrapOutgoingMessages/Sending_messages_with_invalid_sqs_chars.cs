namespace NServiceBus.TransportTests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Transport;

    public class Sending_messages_with_invalid_sqs_chars : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        public async Task Should_receive_message(
            TransportTransactionMode transactionMode)
        {
            var messageProcessed = CreateTaskCompletionSource<MessageContext>();

            await StartPump(
                (context, _) => messageProcessed.SetCompleted(context),
                (_, __) => Task.FromResult(ErrorHandleResult.Handled),
                TransportTransactionMode.None);

            var headers = new Dictionary<string, string>
            {
                { "SomeHeader", "doesn't matter" },
            };

            await SendMessage(InputQueueName, headers, body: "body with invalid chars: \0"u8.ToArray());

            var messageContext = await messageProcessed.Task;

            Assert.That(messageContext.Headers, Is.Not.Empty);
            Assert.That(messageContext.Headers, Is.SupersetOf(headers));
        }
    }
}