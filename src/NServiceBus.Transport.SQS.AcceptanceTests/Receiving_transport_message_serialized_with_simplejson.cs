namespace NServiceBus.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Amazon.SQS.Model;
    using EndpointTemplates;
    using NUnit.Framework;

    public class Receiving_transport_message_serialized_with_simplejson : NServiceBusAcceptanceTest
    {
        // This is a payload that was created using a previous version that still used SimpleJson.
        // In practice this test wouldn't be really needed since the transport message only contains very simple data
        // types but we want to be extra careful.
        // The actual message content doesn't really matter
        const string PayloadSerializedWithSimpleJson = @"{""Headers"":{""NServiceBus.MessageId"":""6637ac9d-2af5-4900-8f23-af9c00eb9918"",""NServiceBus.MessageIntent"":""Send"",""NServiceBus.ConversationId"":""16e18ac2-92ca-495a-9fcd-af9c00eb9922"",""NServiceBus.CorrelationId"":""6637ac9d-2af5-4900-8f23-af9c00eb9918"",""NServiceBus.ReplyToAddress"":""ATBFYA7YY5MEIE8MFHSGENCWSendingInCompatibilityMode-Sender"",""NServiceBus.OriginatingMachine"":""somecomputer"",""NServiceBus.OriginatingEndpoint"":""ReceivingTransportMessageSerializedWithSimplejson.Sender"",""$.diagnostics.originating.hostid"":""4de9545ac822a29fb4bfb9a70d0cea92"",""NServiceBus.ContentType"":""text/xml"",""NServiceBus.EnclosedMessageTypes"":""NServiceBus.AcceptanceTests.Receiving_transport_message_serialized_with_simplejson+Message, NServiceBus.Transport.SQS.AcceptanceTests"",""NServiceBus.Version"":""8.0.3"",""NServiceBus.TimeSent"":""2023-02-01 14:17:47:234185 Z""},""Body"":""PD94bWwgdmVyc2lvbj0iMS4wIj8+PE1lc3NhZ2UgeG1sbnM6eHNpPSJodHRwOi8vd3d3LnczLm9yZy8yMDAxL1hNTFNjaGVtYS1pbnN0YW5jZSIgeG1sbnM6eHNkPSJodHRwOi8vd3d3LnczLm9yZy8yMDAxL1hNTFNjaGVtYSIgeG1sbnM9Imh0dHA6Ly90ZW1wdXJpLm5ldC9OU2VydmljZUJ1cy5BY2NlcHRhbmNlVGVzdHMiPjwvTWVzc2FnZT4="",""S3BodyKey"":null}";

        [Test]
        public async Task Should_work() =>
            await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(c => c.When(async _ =>
                {
                    await SendTo<Receiver>(PayloadSerializedWithSimpleJson);
                }))
                .Done(c => c.Received)
                .Run();

        static async Task SendTo<TEndpoint>(string message)
        {
            using var sqsClient = ConfigureEndpointSqsTransport.CreateSqsClient();
            var getQueueUrlResponse = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest
            {
                QueueName = TestNameHelper.GetSqsQueueName(Conventions.EndpointNamingConvention(typeof(TEndpoint)), SetupFixture.NamePrefix)
            }).ConfigureAwait(false);

            var sendMessageRequest = new SendMessageRequest
            {
                QueueUrl = getQueueUrlResponse.QueueUrl,
                MessageBody = message
            };

            await sqsClient.SendMessageAsync(sendMessageRequest).ConfigureAwait(false);
        }

        public class Context : ScenarioContext
        {
            public bool Received { get; set; }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServer>();

            public class MyMessageHandler : IHandleMessages<Message>
            {
                public MyMessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    testContext.Received = true;
                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }

        }

        public class Message : ICommand
        {
        }
    }
}