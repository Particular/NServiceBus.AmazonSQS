namespace NServiceBus.AcceptanceTests.Sending
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NUnit.Framework;
    using Transport.SQS.Tests;

    public class When_using_large_message_with_kms_encrypted_bucket : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_receive_message()
        {
            var payloadToSend = new byte[PayloadSize];

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When(session => session.SendLocal(new MyMessageWithLargePayload
                {
                    Payload = payloadToSend
                }))
                )
                .Done(c => c.ReceivedPayload != null)
                .Run();

            Assert.AreEqual(payloadToSend, context.ReceivedPayload, "The large payload should be handled correctly using the kms encrypted S3 bucket");

            using var s3Client = ClientFactories.CreateS3Client();

            Assert.DoesNotThrowAsync(async () => await s3Client.GetObjectAsync(BucketName, $"{ConfigureEndpointSqsTransport.S3Prefix}/{context.MessageId}"));
        }

        const int PayloadSize = 150 * 1024;

        static string BucketName;

        public class Context : ScenarioContext
        {
            public byte[] ReceivedPayload { get; set; }
            public string MessageId { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint() =>
                EndpointSetup<DefaultServer>(c =>
                {
                    var transportConfig = c.ConfigureSqsTransport();

                    BucketName = $"{ConfigureEndpointSqsTransport.S3BucketName}.kms";

                    transportConfig.S3 = new S3Settings(BucketName, ConfigureEndpointSqsTransport.S3Prefix, ClientFactories.CreateS3Client());
                });

            public class MyMessageHandler : IHandleMessages<MyMessageWithLargePayload>
            {
                public MyMessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MyMessageWithLargePayload messageWithLargePayload, IMessageHandlerContext context)
                {
                    testContext.MessageId = context.MessageId;
                    testContext.ReceivedPayload = messageWithLargePayload.Payload;

                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }
        }

        public class MyMessageWithLargePayload : ICommand
        {
            public byte[] Payload { get; set; }
        }
    }
}