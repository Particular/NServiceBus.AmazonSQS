using System;
using NServiceBus;
using NUnit.Framework;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Customization;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Configuration.AdvancedExtensibility;

public class Sending_messages_larger_than_256kb : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_receive_messages_with_large_payload_correctly()
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

        Assert.AreEqual(payloadToSend, context.ReceivedPayload, "The large payload should be handled correctly using S3");
    }

    [Test]
    public async Task Should_fail_when_no_s3_bucket_is_configured()
    {
        var payloadToSend = new byte[PayloadSize];

        var context = await Scenario.Define<Context>()
            .WithEndpoint<Sender>(b =>
            {
                // Don't configure an S3 bucket for this endpoint
                b.CustomConfig(x =>
                {
                    x.GetSettings().Set("NServiceBus.AmazonSQS.S3BucketForLargeMessages", string.Empty);
                    x.GetSettings().Set("NServiceBus.AmazonSQS.S3Key", string.Empty);
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
                    x.GetSettings().Set("NServiceBus.AmazonSQS.S3BucketForLargeMessages", string.Empty);
                    x.GetSettings().Set("NServiceBus.AmazonSQS.S3Key", string.Empty);
                });
            })
            .Done(c => c.GotTheException)
            .Run();

        Assert.AreEqual(context.Exception.Message, "Cannot send large message because no S3 bucket was configured. Add an S3 bucket name to your configuration.");
    }

    const int PayloadSize = 257 * 1024;

    public class Context : ScenarioContext
    {
        public byte[] ReceivedPayload { get; set; }

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

                return Task.FromResult(0);
            }
        }

    }

    public class MyMessageWithLargePayload : ICommand
    {
        public byte[] Payload { get; set; }
    }
}