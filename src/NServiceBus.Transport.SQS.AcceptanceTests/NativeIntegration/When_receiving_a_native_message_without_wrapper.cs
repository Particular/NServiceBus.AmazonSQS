namespace NServiceBus.AcceptanceTests.NativeIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using AcceptanceTesting;
    using Amazon.S3.Model;
    using Amazon.SQS.Model;
    using EndpointTemplates;
    using NUnit.Framework;
    using Transport.SQS.Tests;

    public class When_receiving_a_native_message_without_wrapper : NServiceBusAcceptanceTest
    {
        static readonly string MessageToSend = new XDocument(new XElement("NServiceBus.AcceptanceTests.NativeIntegration.NativeMessage", new XElement("ThisIsTheMessage", "Hello!"))).ToString();

        [Test]
        public async Task Should_be_processed_when_nsbheaders_present_with_messageid()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(c => c.When(async _ =>
                {
                    await NativeEndpoint.SendTo<Receiver>(
                        new Dictionary<string, MessageAttributeValue>
                        {
                            {
                                "NServiceBus.AmazonSQS.Headers",
                                new MessageAttributeValue
                                {
                                    DataType = "String", StringValue = GetHeaders(messageId: Guid.NewGuid().ToString())
                                }
                            }
                        }, MessageToSend, false);
                }))
                .Done(c => c.MessageReceived != null)
                .Run();

            Assert.AreEqual("Hello!", context.MessageReceived);
        }

        [Test]
        public async Task Should_be_processed_when_nsbheaders_present_without_messageid()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(c => c.When(async _ =>
                {
                    await NativeEndpoint.SendTo<Receiver>(
                        new Dictionary<string, MessageAttributeValue>
                        {
                            {
                                "NServiceBus.AmazonSQS.Headers",
                                new MessageAttributeValue
                                {
                                    DataType = "String", StringValue = GetHeaders()
                                }
                            }
                        }, MessageToSend, false);
                }))
                .Done(c => c.MessageReceived != null)
                .Run();

            Assert.AreEqual("Hello!", context.MessageReceived);
        }

        [Test]
        public async Task Should_be_processed_without_nsbheaders_or_messageid()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(c => c.When(async _ =>
                {
                    await NativeEndpoint.SendTo<Receiver>(null, MessageToSend, false);
                }))
                .Done(c => c.MessageReceived != null)
                .Run();

            Assert.AreEqual("Hello!", context.MessageReceived);
        }

        [Test]
        public async Task Should_support_loading_body_from_s3()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(c => c.When(async _ =>
                {
                    var key = Guid.NewGuid().ToString();
                    await UploadMessageBodyToS3(key);
                    await NativeEndpoint.SendTo<Receiver>(
                        new Dictionary<string, MessageAttributeValue>
                        {
                            {
                                "NServiceBus.AmazonSQS.Headers",
                                new MessageAttributeValue
                                {
                                    DataType = "String", StringValue = GetHeaders(s3Key: key)
                                }
                            }
                        }, MessageToSend, false);
                }))
                .Done(c => c.MessageReceived != null)
                .Run();

            Assert.AreEqual("Hello!", context.MessageReceived);
        }

        string GetHeaders(string s3Key = null, string messageId = null)
        {
            var nsbHeaders = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(s3Key))
            {
                nsbHeaders.Add("S3BodyKey", "s3Key");
            }

            if (!string.IsNullOrEmpty(messageId))
            {
                nsbHeaders.Add("NServiceBus.MessageId", messageId);
            }

            return JsonSerializer.Serialize(nsbHeaders);
        }

        static async Task UploadMessageBodyToS3(string key)
        {
            using var s3Client = ClientFactories.CreateS3Client();
            await s3Client.PutObjectAsync(new PutObjectRequest
            {
                Key = key,
                BucketName = ConfigureEndpointSqsTransport.S3BucketName,
                ContentBody = MessageToSend
            });
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServer>();

            class MyHandler : IHandleMessages<NativeMessage>
            {
                public MyHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(NativeMessage message, IMessageHandlerContext context)
                {
                    testContext.MessageReceived = message.ThisIsTheMessage;

                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }
        }

        class Context : ScenarioContext
        {
            public string MessageReceived { get; set; }
        }
    }
    public class NativeMessage : IMessage
    {
        public string ThisIsTheMessage { get; set; }
    }
}