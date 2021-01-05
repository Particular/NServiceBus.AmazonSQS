namespace NServiceBus.AcceptanceTests.NativeIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Amazon.S3.Model;
    using Amazon.SQS.Model;
    using Configuration.AdvancedExtensibility;
    using EndpointTemplates;
    using NUnit.Framework;
    using Recoverability;
    using Settings;
    using Transport.SQS;

    public class When_receiving_a_native_message : NServiceBusAcceptanceTest
    {
        static readonly string MessageToSend = new XDocument(new XElement("Message", new XElement("ThisIsTheMessage", "Hello!"))).ToString();

        [Test]
        public async Task Should_be_processed_when_messagetypefullname_present()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(c => c.When(async _ =>
                {
                    await SendNativeMessage(new Dictionary<string, MessageAttributeValue>
                    {
                        {"MessageTypeFullName", new MessageAttributeValue {DataType = "String", StringValue = typeof(Message).FullName}}
                    });
                }))
                .Done(c => c.MessageReceived != null)
                .Run();

            Assert.AreEqual("Hello!", context.MessageReceived);
        }

        [Test]
        public async Task Should_fail_when_messagetypefullname_not_present()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            try
            {
                await Scenario.Define<Context>()
                    .WithEndpoint<Receiver>(c =>
                    {
                        c.CustomConfig((cfg, ctx) =>
                        {
                            ctx.ErrorQueueAddress = cfg.GetSettings().ErrorQueueAddress();
                        });
                        c.When(async (session, ctx) =>
                        {
                            await SendNativeMessage(new Dictionary<string, MessageAttributeValue>
                            {
                                // unfortunately only the message id attribute is preserved when moving to the poison queue
                                { Headers.MessageId, new MessageAttributeValue {DataType = "String", StringValue = ctx.TestRunId.ToString()} },
                            });
                            _ = CheckErrorQueue(ctx, cancellationTokenSource.Token);
                        }).DoNotFailOnErrorMessages();
                    })
                    .Done(c => c.MessageMovedToPoisonQueue)
                    .Run();
            }
            catch (TimeoutException)
            {
                cancellationTokenSource.Cancel();
            }
        }

        [Test]
        public async Task Should_support_loading_body_from_s3()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(c => c.When(async _ =>
                {
                    var key = Guid.NewGuid().ToString();
                    await UploadMessageBodyToS3(key);
                    await SendNativeMessage(new Dictionary<string, MessageAttributeValue>
                    {
                        {"MessageTypeFullName", new MessageAttributeValue {DataType = "String", StringValue = typeof(Message).FullName}},
                        {"S3BodyKey", new MessageAttributeValue {DataType = "String", StringValue = key}},
                    });
                }))
                .Done(c => c.MessageReceived != null)
                .Run();

            Assert.AreEqual("Hello!", context.MessageReceived);
        }

        async Task CheckErrorQueue(Context context, CancellationToken cancellationToken)
        {
            var transport = new TransportExtensions<SqsTransport>(new SettingsHolder());
            transport = transport.ConfigureSqsTransport(SetupFixture.NamePrefix);
            var transportConfiguration = new TransportConfiguration(transport.GetSettings());
            using (var sqsClient = SqsTransportExtensions.CreateSQSClient())
            {
                var getQueueUrlResponse = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest
                {
                    QueueName = QueueCache.GetSqsQueueName(context.ErrorQueueAddress, transportConfiguration)
                }, cancellationToken).ConfigureAwait(false);

                ReceiveMessageResponse receiveMessageResponse = null;

                while (context.MessageMovedToPoisonQueue == false && !cancellationToken.IsCancellationRequested)
                {
                    receiveMessageResponse = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                    {
                        QueueUrl = getQueueUrlResponse.QueueUrl,
                        WaitTimeSeconds = 20,
                        MessageAttributeNames = new List<string> { Headers.MessageId }
                    }, cancellationToken).ConfigureAwait(false);

                    foreach (var msg in receiveMessageResponse.Messages)
                    {
                        msg.MessageAttributes.TryGetValue(Headers.MessageId, out var messageIdAttribute);
                        if (messageIdAttribute?.StringValue == context.TestRunId.ToString())
                        {
                            context.MessageMovedToPoisonQueue = true;
                        }
                    }
                }

                Assert.NotNull(receiveMessageResponse);
            }
        }

        static async Task SendNativeMessage(Dictionary<string, MessageAttributeValue> messageAttributeValues)
        {
            var transport = new TransportExtensions<SqsTransport>(new SettingsHolder());
            transport = transport.ConfigureSqsTransport(SetupFixture.NamePrefix);
            var transportConfiguration = new TransportConfiguration(transport.GetSettings());
            using (var sqsClient = SqsTransportExtensions.CreateSQSClient())
            {
                var getQueueUrlResponse = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest
                {
                    QueueName = QueueCache.GetSqsQueueName(Conventions.EndpointNamingConvention(typeof(Receiver)), transportConfiguration)
                }).ConfigureAwait(false);

                var sendMessageRequest = new SendMessageRequest
                {
                    QueueUrl = getQueueUrlResponse.QueueUrl,
                    MessageBody = Convert.ToBase64String(Encoding.UTF8.GetBytes(MessageToSend)),
                    MessageAttributes = messageAttributeValues
                };
                await sqsClient.SendMessageAsync(sendMessageRequest).ConfigureAwait(false);
            }
        }

        static async Task UploadMessageBodyToS3(string key)
        {
            using (var s3Client = SqsTransportExtensions.CreateS3Client())
            {
                await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    Key = key,
                    BucketName = SqsTransportExtensions.S3BucketName,
                    ContentBody = MessageToSend
                });
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>();
            }

            class MyEventHandler : IHandleMessages<Message>
            {
                public MyEventHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    testContext.MessageReceived = message.ThisIsTheMessage;

                    return Task.CompletedTask;
                }

                private Context testContext;
            }
        }

        public class Message : IMessage
        {
            public string ThisIsTheMessage { get; set; }
        }

        class Context : ScenarioContext
        {
            public string ErrorQueueAddress { get; set; }
            public string MessageReceived { get; set; }
            public bool MessageMovedToPoisonQueue { get; set; }
        }
    }
}