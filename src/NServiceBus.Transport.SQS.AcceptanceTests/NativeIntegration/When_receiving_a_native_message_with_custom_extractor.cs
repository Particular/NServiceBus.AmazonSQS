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
        public async Task Should_be_processed_if_extractor_is_valid()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<ReceiverWithValidMessageExtractor>(c => c.When(async _ =>
                {
                    await NativeEndpoint.SendTo<ReceiverWithValidMessageExtractor>(new Dictionary<string, MessageAttributeValue>
                    {
                        {CustomHeader, new MessageAttributeValue {DataType = "String", StringValue = typeof(Message).FullName}}
                    }, MessageToSend);
                }))
                .Done(c => c.MessageReceived != null)
                .Run();

            Assert.AreEqual("Hello!", context.MessageReceived);
        }

        public class ReceiverWithValidMessageExtractor : EndpointConfigurationBuilder
        {
            public ReceiverWithValidMessageExtractor()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ConfigureSqsTransport().MessageExtractor = new CustomMessageExtractor();
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
            public Context()
            {

            }
            public string MessageReceived { get; set; }
        }

        public class CustomMessageExtractor : IMessageExtractor
        {
            public bool TryExtractIncomingMessage(Amazon.SQS.Model.Message receivedMessage, out Dictionary<string, string> headers, out string body)
            {
                if (receivedMessage.MessageAttributes.TryGetValue(CustomHeader, out var _))
                {
                    headers = new Dictionary<string, string>
                {
                    { Headers.EnclosedMessageTypes,  typeof(Message).FullName}
                };

                    body = receivedMessage.Body;

                    return true;
                }

                headers = default;
                body = default;

                return false;
            }
        }
    }
}