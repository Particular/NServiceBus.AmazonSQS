namespace NServiceBus.AcceptanceTests.Sending
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;

    public class Sending_large_message_using_unencrypted_bucket : NServiceBusAcceptanceTest
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

            Assert.AreEqual(payloadToSend, context.ReceivedPayload, "The large payload should be handled correctly using the unencrypted S3 bucket");

            var s3Client = SqsTransportExtensions.CreateS3Client();

            Assert.DoesNotThrowAsync(async () => await s3Client.GetObjectAsync(SqsTransportExtensions.S3BucketName, $"{SqsTransportExtensions.S3Prefix}/{context.MessageId}"));
        }

        const int PayloadSize = 150 * 1024;

        public class Context : ScenarioContext
        {
            public byte[] ReceivedPayload { get; set; }
            public string MessageId { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(c => c.UseTransport<SqsTransport>()
                    .S3(SqsTransportExtensions.S3BucketName, SqsTransportExtensions.S3Prefix));
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