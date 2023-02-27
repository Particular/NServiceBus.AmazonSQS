namespace NServiceBus.AcceptanceTests.Sending
{
    using System;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Amazon.S3;
    using Amazon.S3.Model;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_using_large_message_with_customer_provided_aes : NServiceBusAcceptanceTest
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
            var getObjectResponse = await s3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = BucketName,
                Key = $"{SqsTransportExtensions.S3Prefix}/{context.MessageId}",
                ServerSideEncryptionCustomerMethod = ServerSideEncryptionCustomerMethod.AES256,
                ServerSideEncryptionCustomerProvidedKey = Base64Key,
            });

            Assert.AreEqual(ServerSideEncryptionCustomerMethod.AES256, getObjectResponse.ServerSideEncryptionCustomerMethod);
            Assert.IsNull(getObjectResponse.ServerSideEncryptionMethod);
        }

        const int PayloadSize = 150 * 1024;

        static string BucketName;
        static string Base64Key;

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

                    var aesEncryption = Aes.Create();
                    aesEncryption.KeySize = 256;
                    aesEncryption.GenerateKey();
                    Base64Key = Convert.ToBase64String(aesEncryption.Key);

                    var s3Config = transportConfig.S3(BucketName, SqsTransportExtensions.S3Prefix);
                    s3Config.ServerSideCustomerEncryption(ServerSideEncryptionCustomerMethod.AES256, Base64Key);
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