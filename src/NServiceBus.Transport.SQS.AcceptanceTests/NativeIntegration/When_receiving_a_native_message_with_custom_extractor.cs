namespace NServiceBus.AcceptanceTests.NativeIntegration
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using AcceptanceTesting;
    using Amazon.SQS.Model;
    using EndpointTemplates;
    using NServiceBus.Transport.SQS;
    using NUnit.Framework;

    public class When_receiving_a_native_message_with_custom_extractor : NServiceBusAcceptanceTest
    {
        static readonly string MessageToSend = new XDocument(new XElement("Message", new XElement("ThisIsTheMessage", "Hello!"))).ToString();
        static readonly string CustomHeader = "MyCustomHeader";

        [Test]
        public async Task Should_be_processed()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(c => c.When(async _ =>
                {
                    await NativeEndpoint.SendTo<Receiver>(new Dictionary<string, MessageAttributeValue>
                    {
                        {CustomHeader, new MessageAttributeValue {DataType = "String", StringValue = typeof(Message).FullName}}
                    }, MessageToSend);
                }))
                .Done(c => c.MessageReceived != null)
                .Run();

            Assert.AreEqual("Hello!", context.MessageReceived);
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ConfigureSqsTransport().IncomingMessageExtractor = new CustomMessageExtractor();
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
            public string MessageReceived { get; set; }
        }

        public class CustomMessageExtractor : IAmazonSqsIncomingMessageExtractor
        {
            public bool TryExtractMessage(Amazon.SQS.Model.Message receivedMessage, string messageId, out Dictionary<string, string> headers, out string s3BodyKey, out string body)
            {
                if (receivedMessage.MessageAttributes.TryGetValue(CustomHeader, out var _))
                {
                    headers = new Dictionary<string, string>
                {
                    { Headers.MessageId, messageId },
                    { Headers.EnclosedMessageTypes,  typeof(Message).FullName}
                };

                    body = receivedMessage.Body;
                    s3BodyKey = default;

                    return true;
                }

                headers = default;
                s3BodyKey = default;
                body = default;

                return false;
            }
        }
    }
}