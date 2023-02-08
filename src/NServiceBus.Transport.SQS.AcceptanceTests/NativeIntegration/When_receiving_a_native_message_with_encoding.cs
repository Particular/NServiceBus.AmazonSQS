namespace NServiceBus.AcceptanceTests.NativeIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using AcceptanceTesting;
    using Amazon.S3.Model;
    using Amazon.SQS.Model;
    using Configuration.AdvancedExtensibility;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_receiving_a_native_message_with_encoding : NServiceBusAcceptanceTest
    {
        static readonly string MessageToSend = new XDocument(new XElement("Message", new XElement("ThisIsTheMessage", "Hello!"))).ToString();

        [Test]
        public async Task Should_be_processed_when_messagetypefullname_present()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(c => c.When(async _ =>
                {
                    await NativeEndpoint.SendTo<Receiver>(new Dictionary<string, MessageAttributeValue>
                    {
                        {"MessageTypeFullName", new MessageAttributeValue {DataType = "String", StringValue = typeof(Message).FullName}}
                    }, MessageToSend);
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
                            await NativeEndpoint.SendTo<Receiver>(new Dictionary<string, MessageAttributeValue>
                            {
                                // unfortunately only the message id attribute is preserved when moving to the poison queue
                                {
                                    Headers.MessageId, new MessageAttributeValue {DataType = "String", StringValue = ctx.TestRunId.ToString()}
                                }
                            }, MessageToSend);
                            _ = NativeEndpoint.ConsumePoisonQueue(ctx.TestRunId, ctx.ErrorQueueAddress, _ =>
                            {
                                ctx.MessageMovedToPoisonQueue = true;
                            }, cancellationTokenSource.Token);
                        }).DoNotFailOnErrorMessages();
                    })
                    .Done(c => c.MessageMovedToPoisonQueue)
                    .Run();
            }
            finally
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
                    await NativeEndpoint.SendTo<Receiver>(new Dictionary<string, MessageAttributeValue>
                    {
                        {"MessageTypeFullName", new MessageAttributeValue {DataType = "String", StringValue = typeof(Message).FullName}},
                        {"S3BodyKey", new MessageAttributeValue {DataType = "String", StringValue = key}},
                    }, MessageToSend);
                }))
                .Done(c => c.MessageReceived != null)
                .Run();

            Assert.AreEqual("Hello!", context.MessageReceived);
        }

        static async Task UploadMessageBodyToS3(string key)
        {
            using var s3Client = ConfigureEndpointSqsTransport.CreateS3Client();
            await s3Client.PutObjectAsync(new PutObjectRequest
            {
                Key = key,
                BucketName = ConfigureEndpointSqsTransport.S3BucketName,
                ContentBody = MessageToSend
            });
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ConfigureSqsTransport().S3 = new S3Settings(ConfigureEndpointSqsTransport.S3BucketName, ConfigureEndpointSqsTransport.S3Prefix, ConfigureEndpointSqsTransport.CreateS3Client());
                });
            }

            class MyHandler : IHandleMessages<Message>
            {
                public MyHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    testContext.MessageReceived = message.ThisIsTheMessage;

                    return Task.CompletedTask;
                }

                Context testContext;
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