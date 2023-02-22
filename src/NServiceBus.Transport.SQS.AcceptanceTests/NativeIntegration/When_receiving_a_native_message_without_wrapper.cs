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

    public class When_receiving_a_native_message_without_wrapper : NServiceBusAcceptanceTest
    {
        static readonly string MessageToSend = new XDocument(new XElement("NServiceBus.AcceptanceTests.NativeIntegration.NativeMessage", new XElement("ThisIsTheMessage", "Hello!"))).ToString();
        //NOTE the unicode gets converted to utf-16 by .net automatically
        //static readonly string MessageToSendUnicode = new XDocument(new XElement("NServiceBus.AcceptanceTests.NativeIntegration.NativeMessage", new XElement("ThisIsTheMessage", @"This Unicode string contains two characters with codes outside the traditional ASCII code range, Pi (\u03a0) and Sigma (\u03a3)."))).ToString();
        //static readonly string MessageToSendUnicode = new XDocument(new XElement("NServiceBus.AcceptanceTests.NativeIntegration.NativeMessage", new XElement("ThisIsTheMessage", "\u00abX\u00bb"))).ToString();

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

        //[Test]
        //public async Task Should_be_processed_when_nsbheaders_present_with_encoding_unicode_specified()
        //{
        //    var context = await Scenario.Define<Context>()
        //        .WithEndpoint<Receiver>(c => c.When(async _ =>
        //        {
        //            await NativeEndpoint.SendTo<Receiver>(
        //                new Dictionary<string, MessageAttributeValue>
        //                {
        //                    {
        //                        "NServiceBus.AmazonSQS.Headers",
        //                        new MessageAttributeValue
        //                        {
        //                            DataType = "String", StringValue = GetHeaders(messageId: Guid.NewGuid().ToString(), encoding: "unicode")
        //                        }
        //                    }
        //                }, MessageToSendUnicode, false);
        //        }))
        //        .Done(c => c.MessageReceived != null)
        //        .Run();

        //    Assert.AreEqual("Hello", context.MessageReceived);
        //}

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

        string GetHeaders(string s3Key = null, string messageId = null, string encoding = null)
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

            if (!string.IsNullOrEmpty(encoding))
            {
                nsbHeaders.Add("NServiceBus.AmazonSQS.Encoding", encoding);
            }

            return JsonSerializer.Serialize(nsbHeaders);
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