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
    using Configuration.AdvancedExtensibility;
    using Settings;
    using Transport.SQS;

    static class NativeMessage
    {
        public static async Task ErrorQueue(Guid testRunId, string errorQueueAddress, CancellationToken cancellationToken, Action<Message> nativeMessageAccessor = null)
        {
            var transport = new TransportExtensions<SqsTransport>(new SettingsHolder());
            transport = transport.ConfigureSqsTransport(SetupFixture.NamePrefix);
            var transportConfiguration = new TransportConfiguration(transport.GetSettings());
            using (var sqsClient = SqsTransportExtensions.CreateSQSClient())
            {
                var getQueueUrlResponse = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest
                {
                    QueueName = QueueCache.GetSqsQueueName(errorQueueAddress, transportConfiguration)
                }, cancellationToken).ConfigureAwait(false);

                ReceiveMessageResponse receiveMessageResponse = null;

                while (!cancellationToken.IsCancellationRequested)
                {
                    receiveMessageResponse = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
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

                        await sqsClient.DeleteMessageAsync(getQueueUrlResponse.QueueUrl, msg.ReceiptHandle, CancellationToken.None);
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

        public static async Task SendTo<TEndpoint>(Dictionary<string, MessageAttributeValue> messageAttributeValues, string message)
        {
            var transport = new TransportExtensions<SqsTransport>(new SettingsHolder());
            transport = transport.ConfigureSqsTransport(SetupFixture.NamePrefix);
            var transportConfiguration = new TransportConfiguration(transport.GetSettings());
            using (var sqsClient = SqsTransportExtensions.CreateSQSClient())
            {
                var getQueueUrlResponse = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest
                {
                    QueueName = QueueCache.GetSqsQueueName(Conventions.EndpointNamingConvention(typeof(TEndpoint)),
                        transportConfiguration)
                }).ConfigureAwait(false);

                var body = Convert.ToBase64String(Encoding.Unicode.GetBytes(message));

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