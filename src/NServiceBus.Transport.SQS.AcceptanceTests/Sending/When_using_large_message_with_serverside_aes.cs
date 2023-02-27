namespace NServiceBus.AcceptanceTests.Sending
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

            var s3Client = SqsTransportExtensions.CreateS3Client();
            var getObjectResponse = await s3Client.GetObjectAsync(BucketName, $"{SqsTransportExtensions.S3Prefix}/{context.MessageId}");

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
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var transportConfig = c.UseTransport<SqsTransport>();

                    BucketName = $"{SqsTransportExtensions.S3BucketName}";

                    var s3Config = transportConfig.S3(BucketName, SqsTransportExtensions.S3Prefix);
                    s3Config.ServerSideEncryption(ServerSideEncryptionMethod.AES256);
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessageWithLargePayload>
            {
                public Context Context { get; set; }

                public Task Handle(MyMessageWithLargePayload messageWithLargePayload, IMessageHandlerContext context)
                {
                    Context.MessageId = context.MessageId;
                    Context.ReceivedPayload = messageWithLargePayload.Payload;

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