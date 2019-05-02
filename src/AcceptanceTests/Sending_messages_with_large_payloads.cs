namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Linq;
    using NServiceBus;
    using NUnit.Framework;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Amazon.S3;
    using Amazon.S3.Model;
    using AmazonSQS.AcceptanceTests;
    using EndpointTemplates;
    using Configuration.AdvancedExtensibility;

    public class Sending_messages_with_large_payloads : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_receive_messages_with_payload_larger_than_threshold_correctly_with_s3_configured()
        {
            var payloadToSend = new byte[PayloadSize];

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When(session => session.Send(new MyMessageWithLargePayload
                {
                    Payload = payloadToSend
                })))
                .WithEndpoint<Receiver>()
                .Done(c => c.ReceivedPayload != null)
                .Run();

            var s3Client = SqsTransportExtensions.CreateS3Client();
            Assert.AreEqual(payloadToSend, context.ReceivedPayload, "The large payload should be handled correctly using S3");
            Assert.DoesNotThrowAsync(async () => await s3Client.GetObjectAsync(SqsTransportExtensions.S3BucketName, $"{SqsTransportExtensions.S3Prefix}/{context.MessageId}"));
        }
        
        [Test]
        public async Task Should_receive_messages_with_kms_encrypted_payload_correctly()
        {
            var kmsBucketName = $"{SqsTransportExtensions.S3BucketName}.kms";
           
            var payloadToSend = new byte[PayloadSize];
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b =>
                {
                    b.CustomConfig(x =>
                    {
                        x.GetSettings().Set(SettingsKeys.S3BucketForLargeMessages, kmsBucketName);
                    });
                    
                    b.When(session => session.Send(new MyMessageWithLargePayload
                    {
                        Payload = payloadToSend
                    }));
                })
                .WithEndpoint<Receiver>()
                .Done(c => c.ReceivedPayload != null)
                .Run();

            var s3Client = SqsTransportExtensions.CreateS3Client();

            Assert.AreEqual(payloadToSend, context.ReceivedPayload, "The large payload should be handled correctly using S3");
            Assert.DoesNotThrowAsync(async () => await s3Client.GetObjectAsync(kmsBucketName, $"{SqsTransportExtensions.S3Prefix}/{context.MessageId}"));
        }

        [Test]
        public async Task Should_receive_messages_with_payload_smaller_than_threshold_correctly_without_s3_configured()
        {
            var payloadToSend = new byte[PayloadSizeBelowThreshold];

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b =>
                {
                    // Don't configure an S3 bucket for this endpoint
                    b.CustomConfig(x =>
                    {
                        x.GetSettings().Set(SettingsKeys.S3BucketForLargeMessages, string.Empty);
                        x.GetSettings().Set(SettingsKeys.S3KeyPrefix, string.Empty);
                    });

                    b.When(async (session, c) =>
                    {
                        try
                        {
                            await session.Send(new MyMessageWithLargePayload
                            {
                                Payload = payloadToSend
                            });
                        }
                        catch (Exception ex)
                        {
                            c.Exception = ex;
                            c.GotTheException = true;
                        }
                    });
                })
                .WithEndpoint<Receiver>(b =>
                {
                    // Don't configure an S3 bucket for this endpoint
                    b.CustomConfig(x =>
                    {
                        x.GetSettings().Set(SettingsKeys.S3BucketForLargeMessages, string.Empty);
                        x.GetSettings().Set(SettingsKeys.S3KeyPrefix, string.Empty);
                    });
                })
                .Done(c => c.ReceivedPayload != null || c.Exception != null)
                .Run();

            Assert.IsNull(context.Exception);
            Assert.AreEqual(payloadToSend, context.ReceivedPayload, "The large payload should be handled correctly using S3");
        }

        [Test]
        public async Task Should_fail_with_payload_larger_than_threshold_without_s3_configured()
        {
            var payloadToSend = new byte[PayloadSize];

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b =>
                {
                    // Don't configure an S3 bucket for this endpoint
                    b.CustomConfig(x =>
                    {
                        x.GetSettings().Set(SettingsKeys.S3BucketForLargeMessages, string.Empty);
                        x.GetSettings().Set(SettingsKeys.S3KeyPrefix, string.Empty);
                    });

                    b.When(async (session, c) =>
                    {
                        try
                        {
                            await session.Send(new MyMessageWithLargePayload
                            {
                                Payload = payloadToSend
                            });
                        }
                        catch (Exception ex)
                        {
                            c.Exception = ex;
                            c.GotTheException = true;
                        }
                    });
                })
                .WithEndpoint<Receiver>(b =>
                {
                    // Don't configure an S3 bucket for this endpoint
                    b.CustomConfig(x =>
                    {
                        x.GetSettings().Set(SettingsKeys.S3BucketForLargeMessages, string.Empty);
                        x.GetSettings().Set(SettingsKeys.S3KeyPrefix, string.Empty);
                    });
                })
                .Done(c => c.GotTheException)
                .Run();

            Assert.AreEqual(context.Exception.Message, "Cannot send large message because no S3 bucket was configured. Add an S3 bucket name to your configuration.");
        }

        const int PayloadSize = 150 * 1024;
        const int PayloadSizeBelowThreshold = 137 * 1024;

        public class Context : ScenarioContext
        {
            public byte[] ReceivedPayload { get; set; }

            public string MessageId { get; set; }

            public bool GotTheException { get; set; }

            public Exception Exception { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(builder =>
                {
                    builder.ConfigureTransport().Routing().RouteToEndpoint(typeof(MyMessageWithLargePayload), typeof(Receiver));
                });
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessageWithLargePayload>
            {
                public Context Context { get; set; }

                public Task Handle(MyMessageWithLargePayload messageWithLargePayload, IMessageHandlerContext context)
                {
                    Context.ReceivedPayload = messageWithLargePayload.Payload;
                    Context.MessageId = context.MessageId;

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