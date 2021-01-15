namespace NServiceBus.AcceptanceTests.NativeIntegration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Amazon.SQS.Model;
    using Configuration.AdvancedExtensibility;
    using EndpointTemplates;
    using NUnit.Framework;
    using Settings;
    using Transport.SQS;

    public class When_receiving_a_message_with_additional_attributes_on_native_message : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task When_moving_native_message_to_poison_queue_should_copy_message_attributes()
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
                            await SendNativeMessage(new Dictionary<string, MessageAttributeValue>
                            {
                                // unfortunately only the message id attribute is preserved when moving to the poison queue
                                { Headers.MessageId, new MessageAttributeValue {DataType = "String", StringValue = ctx.TestRunId.ToString() }},
                                {"SomethingRandom", new MessageAttributeValue {DataType = "String", StringValue = "bla"}},
                                {TransportHeaders.S3BodyKey, new MessageAttributeValue {DataType = "String", StringValue = "bla"}}
                            },
                            null);
                            _ = CheckErrorQueue(ctx, cancellationTokenSource.Token);
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

        [Test]
        public async Task When_moving_message_to_error_queue_should_copy_message_attributes()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            try
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
                            await SendNativeMessage(new Dictionary<string, MessageAttributeValue>
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
            finally
            {
                cancellationTokenSource.Cancel();
            }
        }

        [Test]
        public async Task When_message_moves_through_delayed_delivery_queue_should_copy_message_attributes()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            try
            {
                var delay = QueueDelayTime.Add(TimeSpan.FromSeconds(1));
                var testContext = await Scenario.Define<Context>()
                    .WithEndpoint<Receiver>(c =>
                    {
                        c.CustomConfig((cfg, ctx) =>
                        {
                            cfg.SendFailedMessagesTo(Conventions.EndpointNamingConvention(typeof(ErrorSpy)));
                            cfg.Recoverability().Delayed(settings =>
                            {
                                settings.NumberOfRetries(1);
                                settings.TimeIncrease(delay);
                            });
                            cfg.Recoverability().Immediate(settings => settings.NumberOfRetries(0));
                            cfg.ConfigureSqsTransport().UnrestrictedDurationDelayedDelivery(QueueDelayTime);
                        });
                        c.When(async (session, ctx) =>
                        {
                            await SendNativeMessage(new Dictionary<string, MessageAttributeValue>
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

        [Test]
        public async Task When_message_moves_through_delayed_delivery_queue_multiple_times_should_copy_message_attributes()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            try
            {
                var delay = QueueDelayTime.Add(QueueDelayTime).Add(TimeSpan.FromSeconds(1));
                var testContext = await Scenario.Define<Context>()
                    .WithEndpoint<Receiver>(c =>
                    {
                        c.CustomConfig((cfg, ctx) =>
                        {
                            cfg.SendFailedMessagesTo(Conventions.EndpointNamingConvention(typeof(ErrorSpy)));
                            cfg.Recoverability().Delayed(settings =>
                            {
                                settings.NumberOfRetries(1);
                                settings.TimeIncrease(delay);
                            });
                            cfg.Recoverability().Immediate(settings => settings.NumberOfRetries(0));
                            cfg.ConfigureSqsTransport().UnrestrictedDurationDelayedDelivery(QueueDelayTime);
                        });
                        c.When(async (session, ctx) =>
                        {
                            await SendNativeMessage(new Dictionary<string, MessageAttributeValue>
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

        static readonly TimeSpan QueueDelayTime = TimeSpan.FromSeconds(1);
        static async Task SendNativeMessage(Dictionary<string, MessageAttributeValue> messageAttributeValues, Message theMessage)
        {
            var transport = new TransportExtensions<SqsTransport>(new SettingsHolder());
            transport = transport.ConfigureSqsTransport(SetupFixture.NamePrefix);
            var transportConfiguration = new TransportConfiguration(transport.GetSettings());
            using (var sqsClient = SqsTransportExtensions.CreateSQSClient())
            {
                var getQueueUrlResponse = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest
                {
                    QueueName = QueueCache.GetSqsQueueName(Conventions.EndpointNamingConvention(typeof(Receiver)),
                        transportConfiguration)
                }).ConfigureAwait(false);

                string body;
                if (theMessage == null)
                {
                    body = "this is a poison message body";
                }
                else
                {
                    using (var sw = new StringWriter())
                    {
                        var serializer = new System.Xml.Serialization.XmlSerializer( typeof(Message));
                        serializer.Serialize(sw, theMessage);
                        body = Convert.ToBase64String(Encoding.Unicode.GetBytes(sw.ToString()));
                    }
                }

                var sendMessageRequest = new SendMessageRequest
                {
                    QueueUrl = getQueueUrlResponse.QueueUrl,
                    MessageAttributes = messageAttributeValues,
                    MessageBody = body
                };

                await sqsClient.SendMessageAsync(sendMessageRequest).ConfigureAwait(false);
            }
        }

        async Task CheckErrorQueue(Context context, CancellationToken cancellationToken)
        {
            var transport = new TransportExtensions<SqsTransport>(new SettingsHolder());
            transport = transport.ConfigureSqsTransport(SetupFixture.NamePrefix);
            var transportConfiguration = new TransportConfiguration(transport.GetSettings());
            using (var sqsClient = SqsTransportExtensions.CreateSQSClient())
            {
                var getQueueUrlResponse = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest
                {
                    QueueName = QueueCache.GetSqsQueueName(context.ErrorQueueAddress, transportConfiguration)
                }, cancellationToken).ConfigureAwait(false);

                ReceiveMessageResponse receiveMessageResponse = null;

                while (context.MessageMovedToPoisonQueue == false && !cancellationToken.IsCancellationRequested)
                {
                    receiveMessageResponse = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                    {
                        QueueUrl = getQueueUrlResponse.QueueUrl,
                        WaitTimeSeconds = 20,
                        MessageAttributeNames = new List<string> { "*" }
                    }, cancellationToken).ConfigureAwait(false);

                    foreach (var msg in receiveMessageResponse.Messages)
                    {
                        msg.MessageAttributes.TryGetValue(Headers.MessageId, out var messageIdAttribute);
                        if (messageIdAttribute?.StringValue == context.TestRunId.ToString())
                        {
                            context.MessageMovedToPoisonQueue = true;
                            context.MessageAttributesFoundInNativeMessage = msg.MessageAttributes;
                        }
                    }
                }

                Assert.NotNull(receiveMessageResponse);
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

                    return Task.FromResult(0);
                }

                Context testContext;
            }
        }
    }
}