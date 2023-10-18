namespace NServiceBus.AcceptanceTests.NativeIntegration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting.Customization;
    using Amazon.SQS.Model;
    using Transport.SQS.Tests;

    static class NativeEndpoint
    {
        public static async Task ConsumePoisonQueue(Guid testRunId, string errorQueueAddress, Action<Message> nativeMessageAccessor = null, CancellationToken cancellationToken = default)
        {
            using var sqsClient = ClientFactories.CreateSqsClient();
            var getQueueUrlResponse = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest
            {
                QueueName = TestNameHelper.GetSqsQueueName(errorQueueAddress, SetupFixture.NamePrefix)
            }, cancellationToken).ConfigureAwait(false);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var receiveMessageResponse = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = getQueueUrlResponse.QueueUrl,
                    WaitTimeSeconds = 5,
                    MessageAttributeNames = ["All"]
                }, cancellationToken).ConfigureAwait(false);

                foreach (var msg in receiveMessageResponse.Messages)
                {
                    msg.MessageAttributes.TryGetValue(Headers.MessageId, out var messageIdAttribute);
                    if (messageIdAttribute?.StringValue == testRunId.ToString())
                    {
                        nativeMessageAccessor?.Invoke(msg);
                    }

                    await sqsClient.DeleteMessageAsync(getQueueUrlResponse.QueueUrl, msg.ReceiptHandle, cancellationToken);
                }
            }
        }

        public static async Task SendTo<TEndpoint, TMessage>(Dictionary<string, MessageAttributeValue> messageAttributeValues,
            TMessage message)
            where TMessage : IMessage
        {
            using var sw = new Utf8StringWriter();
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(TMessage));
            serializer.Serialize(sw, message);

            await SendTo<TEndpoint>(messageAttributeValues, sw.ToString());
        }

        sealed class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding => Encoding.UTF8;
        }

        public static async Task SendTo<TEndpoint>(Dictionary<string, MessageAttributeValue> messageAttributeValues, string message, bool base64Encode = true)
        {
            using var sqsClient = ClientFactories.CreateSqsClient();
            var getQueueUrlResponse = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest
            {
                QueueName = TestNameHelper.GetSqsQueueName(Conventions.EndpointNamingConvention(typeof(TEndpoint)), SetupFixture.NamePrefix)
            }).ConfigureAwait(false);

            var body = base64Encode ? Convert.ToBase64String(Encoding.UTF8.GetBytes(message)) : message;

            var sendMessageRequest = new SendMessageRequest
            {
                QueueUrl = getQueueUrlResponse.QueueUrl,
                MessageBody = body
            };

            if (messageAttributeValues != null)
            {
                sendMessageRequest.MessageAttributes = messageAttributeValues;
            }

            await sqsClient.SendMessageAsync(sendMessageRequest).ConfigureAwait(false);
        }
    }
}