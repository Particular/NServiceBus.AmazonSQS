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
            byte[] copyOfTheBody = null;

            await StartPump(
                (context, _) =>
                {
                    // This is crucial due to internal buffer pooling in SQS transport
                    copyOfTheBody = context.Body.ToArray();
                    return messageProcessed.SetCompleted(context);
                },
                (_, __) => Task.FromResult(ErrorHandleResult.Handled),
                TransportTransactionMode.None);

            var headers = new Dictionary<string, string>
            {
                { "SomeHeader", "header value with invalid chars: \0" },
            };

            var body = "body with invalid chars: \0"u8.ToArray();

            await SendMessage(InputQueueName, headers, body: body);

            var messageContext = await messageProcessed.Task;

            Assert.That(messageContext.Headers, Is.Not.Empty);
            Assert.Multiple(() =>
            {
                Assert.That(messageContext.Headers, Is.SupersetOf(headers));
                Assert.That(copyOfTheBody, Is.EquivalentTo(body));
            });
        }
    }
}