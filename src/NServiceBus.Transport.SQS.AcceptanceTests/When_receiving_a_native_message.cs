namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Amazon.S3.Model;
    using Amazon.SQS.Model;
    using Configuration.AdvancedExtensibility;
    using EndpointTemplates;
    using NUnit.Framework;
    using Settings;
    using Transport.SQS;

    public class When_receiving_a_native_message : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_process_it_when_messagetypefullname_attribute_is_available()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(c => c.When(async context =>
                {
                    await SendNativeMessage(new Dictionary<string, MessageAttributeValue>
                    {
                        {"MessageTypeFullName", new MessageAttributeValue {DataType = "String", StringValue = typeof(Message).FullName}}
                    });
                }))
                .Done(c => c.MessageReceived)
                .Run();
        }

        [Test]
        public async Task Should_process_it_when_message_payload_on_s3()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(c => c.When(async context =>
                {
                    var key = Guid.NewGuid().ToString();
                    await UploadMessageBodyToS3(key);
                    await SendNativeMessage(new Dictionary<string, MessageAttributeValue>
                    {
                        {"MessageTypeFullName", new MessageAttributeValue {DataType = "String", StringValue = typeof(Message).FullName}},
                        {"S3BodyKey", new MessageAttributeValue {DataType = "String", StringValue = key}},
                    });
                }))
                .Done(c => c.MessageReceived)
                .Run();
        }

        [Test]
        public async Task Should_fail_when_no_attributes_available()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(c =>
                    c.When(async context => { await SendNativeMessage(new Dictionary<string, MessageAttributeValue>()); }).DoNotFailOnErrorMessages())
                .Done(c => c.FailedMessages.Any())
                .Run();
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

                var xMessage = new XDocument(new XElement("Message", new XElement("ThisIsTheMessage", "Hello!"))).ToString();
                var sendMessageRequest = new SendMessageRequest
                {
                    QueueUrl = getQueueUrlResponse.QueueUrl,
                    MessageBody = Convert.ToBase64String(Encoding.UTF8.GetBytes(xMessage)),
                    MessageAttributes = messageAttributeValues
                };
                await sqsClient.SendMessageAsync(sendMessageRequest).ConfigureAwait(false);
            }
        }

        static async Task UploadMessageBodyToS3(string key)
        {
            var s3Client = SqsTransportExtensions.CreateS3Client();
            var xMessage = new XDocument(new XElement("Message", new XElement("ThisIsTheMessage", "Hello!"))).ToString();
            await s3Client.PutObjectAsync(new PutObjectRequest
            {
                Key = key,
                BucketName = SqsTransportExtensions.S3BucketName,
                ContentBody = xMessage
            });
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
                    testContext.MessageReceived = true;

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
            public bool MessageReceived { get; set; }
        }
    }
}