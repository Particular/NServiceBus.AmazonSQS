﻿namespace NServiceBus.TransportTests
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using NUnit.Framework;
    using Transport;
    using Transport.SQS;
    using Transport.SQS.Tests;

    public class Sending_poison_messages : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        public async Task Should_move_to_error_queue_when_unwrapped_and_cannot_deserialize_headers(TransportTransactionMode transactionMode)
        {
            var onMessageCalled = false;
            var onErrorCalled = false;
            var cancellationTokenSource = new CancellationTokenSource();

            OnTestTimeout(() => cancellationTokenSource.Cancel());

            await StartPump(
                (context, ct) =>
                {
                    onMessageCalled = true;
                    return Task.CompletedTask;
                },
                (context, ct) =>
                {
                    onErrorCalled = true;
                    return Task.FromResult(ErrorHandleResult.Handled);
                }, transactionMode);

            using var sqsClient = ClientFactories.CreateSqsClient();
            var queueUrl = await GetQueueUrl(sqsClient, InputQueueName);

            var sendMessageRequest = new SendMessageRequest
            {
                QueueUrl = queueUrl,
                MessageBody = UnwrappedAndNotRelevantPoisonMessageBody,
                MessageAttributes =
                {
                    [TransportHeaders.Headers] = new MessageAttributeValue
                    {
                        StringValue = "junk:this.will.fail.deserializing",
                        DataType = "String"
                    }
                }
            };

            await sqsClient.SendMessageAsync(sendMessageRequest);

            await CheckErrorQueue(ErrorQueueName, cancellationTokenSource.Token);

            Assert.Multiple(() =>
            {
                Assert.That(onErrorCalled, Is.False, "Poison message should not invoke onError");
                Assert.That(onMessageCalled, Is.False, "Poison message should not invoke onMessage");
            });
        }

        static async Task<string> GetQueueUrl(IAmazonSQS sqsClient, string inputQueueName)
        {
            var getQueueUrlResponse = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest
            {
                QueueName = QueueCache.GetSqsQueueName(inputQueueName, SetupFixture.GetNamePrefix())
            }).ConfigureAwait(false);

            return getQueueUrlResponse.QueueUrl;
        }

        async Task CheckErrorQueue(string errorQueueName, CancellationToken cancellationToken)
        {
            using var sqsClient = ClientFactories.CreateSqsClient();
            var getQueueUrlResponse = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest
            {
                QueueName = QueueCache.GetSqsQueueName(errorQueueName, SetupFixture.GetNamePrefix())
            }, cancellationToken).ConfigureAwait(false);

            var messageReceived = false;
            ReceiveMessageResponse receiveMessageResponse = null;

            while (!messageReceived)
            {
                cancellationToken.ThrowIfCancellationRequested();

                receiveMessageResponse = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = getQueueUrlResponse.QueueUrl,
                    WaitTimeSeconds = 20
                }, cancellationToken).ConfigureAwait(false);

                if (receiveMessageResponse.Messages.Any())
                {
                    messageReceived = true;
                }
            }

            Assert.That(receiveMessageResponse, Is.Not.Null);
            Assert.That(receiveMessageResponse.Messages, Has.Count.EqualTo(1));
            Assert.That(receiveMessageResponse.Messages.Single().Body, Is.EqualTo(UnwrappedAndNotRelevantPoisonMessageBody));
        }

        const string UnwrappedAndNotRelevantPoisonMessageBody = "The body doesn't matter, this will be treated as an unwrapped message";
    }
}