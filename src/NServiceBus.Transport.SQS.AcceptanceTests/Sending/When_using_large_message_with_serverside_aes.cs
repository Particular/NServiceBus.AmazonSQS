namespace NServiceBus.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Amazon.S3;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_using_large_message_with_serverside_aes : NServiceBusAcceptanceTest
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

            var s3Client = ConfigureEndpointSqsTransport.CreateS3Client();
            var getObjectResponse = await s3Client.GetObjectAsync(BucketName, $"{ConfigureEndpointSqsTransport.S3Prefix}/{context.MessageId}");

            Assert.AreEqual(ServerSideEncryptionMethod.AES256, getObjectResponse.ServerSideEncryptionMethod);
            Assert.AreEqual(ServerSideEncryptionCustomerMethod.None, getObjectResponse.ServerSideEncryptionCustomerMethod);
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

                    BucketName = $"{ConfigureEndpointSqsTransport.S3BucketName}";

                    transportConfig.S3 = new S3Settings(BucketName, ConfigureEndpointSqsTransport.S3Prefix, ConfigureEndpointSqsTransport.CreateS3Client())
                    {
                        Encryption = new S3EncryptionWithManagedKey(ServerSideEncryptionMethod.AES256)
                    };
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