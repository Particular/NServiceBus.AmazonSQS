namespace NServiceBus.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;

    public class Sending_large_message_using_kms_encrypted_bucket : NServiceBusAcceptanceTest
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

            var s3Client = SqsTransportExtensions.CreateS3Client();

            Assert.DoesNotThrowAsync(async () => await s3Client.GetObjectAsync(BucketName, $"{SqsTransportExtensions.S3Prefix}/{context.MessageId}"));
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
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var transportConfig = c.UseTransport<SqsTransport>();

                    BucketName = $"{SqsTransportExtensions.S3BucketName}.kms";

                    transportConfig.S3(BucketName, SqsTransportExtensions.S3Prefix);
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessageWithLargePayload>
            {
                Context testContext;

                public MyMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(MyMessageWithLargePayload messageWithLargePayload, IMessageHandlerContext context)
                {
                    testContext.MessageId = context.MessageId;
                    testContext.ReceivedPayload = messageWithLargePayload.Payload;

                    return Task.FromResult(0);
                }
            }
        }

        public class MyMessageWithLargePayload : ICommand
        {
            public byte[] Payload { get; set; }
        }
    }
}