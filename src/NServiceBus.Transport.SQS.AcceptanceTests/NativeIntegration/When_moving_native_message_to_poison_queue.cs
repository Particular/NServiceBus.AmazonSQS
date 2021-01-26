namespace NServiceBus.AcceptanceTests.NativeIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Amazon.SQS.Model;
    using Configuration.AdvancedExtensibility;
    using EndpointTemplates;
    using NUnit.Framework;
    using Transport.SQS;

    public class When_moving_native_message_to_poison_queue : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_copy_message_attributes()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            try
            {
                var testContext = await Scenario.Define<Context>()
                    .WithEndpoint<Receiver>(c =>
                    {
                        c.CustomConfig((cfg, ctx) =>
                        {
                            ctx.ErrorQueueAddress = cfg.GetSettings().ErrorQueueAddress();
                        });
                        c.When(async (session, ctx) =>
                        {
                            await NativeEndpoint.SendTo<Receiver>(new Dictionary<string, MessageAttributeValue>
                            {
                                // unfortunately only the message id attribute is preserved when moving to the poison queue
                                { Headers.MessageId, new MessageAttributeValue {DataType = "String", StringValue = ctx.TestRunId.ToString() }},
                                {"SomethingRandom", new MessageAttributeValue {DataType = "String", StringValue = "bla"}},
                                {TransportHeaders.S3BodyKey, new MessageAttributeValue {DataType = "String", StringValue = "bla"}}
                            },
                            "This is a poison message");
                            _ = NativeEndpoint.ConsumePoisonQueue(ctx.TestRunId, ctx.ErrorQueueAddress, cancellationTokenSource.Token, nativeMessage =>
                            {
                                ctx.MessageAttributesFoundInNativeMessage = nativeMessage.MessageAttributes;
                                ctx.MessageMovedToPoisonQueue = true;
                            });
                        }).DoNotFailOnErrorMessages();
                    })
                    .Done(c => c.MessageMovedToPoisonQueue)
                    .Run();

                Assert.That(testContext.MessageAttributesFoundInNativeMessage.ContainsKey(TransportHeaders.S3BodyKey), "We expect that when moving to the poison queue, known native headers are not (re)moved.");
                Assert.That(testContext.MessageAttributesFoundInNativeMessage.ContainsKey("SomethingRandom"));
                testContext.MessageAttributesFoundInNativeMessage.TryGetValue("SomethingRandom", out var randomAttribute);
                Assert.That(randomAttribute, Is.Not.Null);
                Assert.That(randomAttribute.StringValue, Is.EqualTo("bla"));
            }
            finally
            {
                cancellationTokenSource.Cancel();
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>();
            }

            class MyEventHandler : IHandleMessages<Message>
            {
                public MyEventHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    if (message.ShouldFail && message.Id == testContext.TestRunId.ToString())
                    {
                        throw new Exception("Something failed");
                    }

                    testContext.MessageReceived = message.ThisIsTheMessage;

                    return Task.CompletedTask;
                }

                Context testContext;
            }
        }

        public class Message : IMessage
        {
            public string ThisIsTheMessage { get; set; }
            public string Id { get; set; }
            public bool ShouldFail { get; set; }
        }

        class Context : ScenarioContext
        {
            public string ErrorQueueAddress { get; set; }
            public string MessageReceived { get; set; }
            public bool MessageMovedToPoisonQueue { get; set; }
            public Dictionary<string, MessageAttributeValue> MessageAttributesFoundInNativeMessage { get; set; }
            public bool MessageFoundInErrorQueue { get; set; }
        }

        class ErrorSpy : EndpointConfigurationBuilder
        {
            public ErrorSpy()
            {
                EndpointSetup<DefaultServer>(config => config.LimitMessageProcessingConcurrencyTo(1));
            }

            class ErrorMessageHandler : IHandleMessages<Message>
            {
                public ErrorMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(Message initiatingMessage, IMessageHandlerContext context)
                {
                    if (initiatingMessage.Id == testContext.TestRunId.ToString())
                    {
                        testContext.MessageFoundInErrorQueue = true;
                        var nativeMessage = context.Extensions.Get<Amazon.SQS.Model.Message>();
                        testContext.MessageAttributesFoundInNativeMessage = nativeMessage.MessageAttributes;
                    }

                    return Task.CompletedTask;
                }

                Context testContext;
            }
        }
    }
}