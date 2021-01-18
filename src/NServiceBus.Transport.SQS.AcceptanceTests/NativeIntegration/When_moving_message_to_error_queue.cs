namespace NServiceBus.AcceptanceTests.NativeIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Amazon.SQS.Model;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_moving_message_to_error_queue : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_copy_message_attributes()
        {
            var testContext = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(c =>
                {
                    c.CustomConfig((cfg, ctx) =>
                    {
                        cfg.SendFailedMessagesTo(Conventions.EndpointNamingConvention(typeof(ErrorSpy)));
                    });
                    c.When(async (session, ctx) =>
                    {
                        await NativeEndpoint.SendTo<Receiver, Message>(new Dictionary<string, MessageAttributeValue>
                            {
                                { Headers.MessageId, new MessageAttributeValue {DataType = "String", StringValue = ctx.TestRunId.ToString() }},
                                {"MessageTypeFullName", new MessageAttributeValue {DataType = "String", StringValue = typeof(Message).FullName}},
                                {"SomethingRandom", new MessageAttributeValue {DataType = "String", StringValue = "bla"}}
                            },
                            new Message
                            {
                                Id = ctx.TestRunId.ToString(),
                                ShouldFail = true,
                                ThisIsTheMessage = "Hello!"
                            });
                    }).DoNotFailOnErrorMessages();
                })
                .WithEndpoint<ErrorSpy>()
                .Done(c => c.MessageFoundInErrorQueue)
                .Run();

            Assert.That(testContext.MessageAttributesFoundInNativeMessage, Is.Not.Null);
            Assert.IsFalse(testContext.MessageAttributesFoundInNativeMessage.ContainsKey("MessageTypeFullName"));
            Assert.That(testContext.MessageAttributesFoundInNativeMessage.ContainsKey("SomethingRandom"));
            testContext.MessageAttributesFoundInNativeMessage.TryGetValue("SomethingRandom", out var randomAttribute);
            Assert.That(randomAttribute, Is.Not.Null);
            Assert.That(randomAttribute.StringValue, Is.EqualTo("bla"));
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

                private Context testContext;
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