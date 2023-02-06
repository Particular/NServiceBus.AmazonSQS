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

    static class NativeEndpoint
    {
        public static async Task ConsumePoisonQueue(Guid testRunId, string errorQueueAddress, Action<Message> nativeMessageAccessor = null, CancellationToken cancellationToken = default)
        {
            using (var sqsClient = ConfigureEndpointSqsTransport.CreateSqsClient())
            {
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
                        MessageAttributeNames = new List<string> { "*" }
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
        }

        public static async Task SendTo<TEndpoint, TMessage>(Dictionary<string, MessageAttributeValue> messageAttributeValues,
            TMessage message)
            where TMessage : IMessage
        {
            using (var sw = new StringWriter())
            {
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(TMessage));
                serializer.Serialize(sw, message);

                await SendTo<TEndpoint>(messageAttributeValues, sw.ToString());
            }
        }

        public static async Task SendTo<TEndpoint>(Dictionary<string, MessageAttributeValue> messageAttributeValues, string message, bool base64Encode = true)
        {
            using (var sqsClient = ConfigureEndpointSqsTransport.CreateSqsClient())
            {
                var getQueueUrlResponse = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest
                {
                    QueueName = TestNameHelper.GetSqsQueueName(Conventions.EndpointNamingConvention(typeof(TEndpoint)), SetupFixture.NamePrefix)
                }).ConfigureAwait(false);

                var body = base64Encode ? Convert.ToBase64String(Encoding.Unicode.GetBytes(message)) : message;

                var sendMessageRequest = new SendMessageRequest
                {
                    QueueUrl = getQueueUrlResponse.QueueUrl,
                    MessageAttributes = messageAttributeValues,
                    MessageBody = body
                };

                await sqsClient.SendMessageAsync(sendMessageRequest).ConfigureAwait(false);
            }
        }
    }
}