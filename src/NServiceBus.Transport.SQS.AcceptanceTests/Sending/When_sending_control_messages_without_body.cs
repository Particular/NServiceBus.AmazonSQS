namespace NServiceBus.AcceptanceTests.Sending
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using Features;
    using NServiceBus.Extensibility;
    using NServiceBus.Pipeline;
    using NServiceBus.Routing;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using Transport;

    class When_sending_control_messages_without_body : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Can_be_sent_and_processed()
        {
            var settings = new SettingsHolder();
            settings.Set(Transport.SQS.Configure.SettingsKeys.DoNotWrapOutgoingMessages, true);

            var transportConfiguration = new Transport.SQS.TransportConfiguration(settings);
            var context = await Scenario.Define<MyContext>(ctx =>
            {
                ctx.DestinationQueueName = Transport.SQS.QueueCache.GetSqsQueueName("SendingControlMessagesWithoutBody.Receiver", transportConfiguration);
                ctx.ControlMessageId = Guid.NewGuid().ToString();
            })
                .WithEndpoint<Sender>()
                .WithEndpoint<Receiver>()
                .Done(ctx => ctx.ControlMessageReceived)
                .Run();

            Assert.That(context.ControlMessageBodyLength, Is.EqualTo(0));
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(cfg => cfg.ConfigureSqsTransport().DoNotWrapOutgoingMessages());
            }

            class DispatchControlMessageAtStartup : Feature
            {
                public DispatchControlMessageAtStartup()
                {
                    EnableByDefault();
                }

                protected override void Setup(FeatureConfigurationContext context)
                {
                    var testContext = context.Settings.Get<MyContext>();
                    context.RegisterStartupTask(sp => new Startup(
                        sp.Build<IDispatchMessages>(),
                        testContext)
                    );
                }

                class Startup : FeatureStartupTask
                {
                    readonly IDispatchMessages dispatcher;
                    readonly MyContext context;

                    public Startup(IDispatchMessages dispatcher, MyContext context)
                    {
                        this.dispatcher = dispatcher;
                        this.context = context;
                    }

                    protected override Task OnStart(IMessageSession session)
                    {
                        var transportOperations = new TransportOperations(
                            new TransportOperation(
                                new OutgoingMessage(
                                    context.ControlMessageId,
                                    new Dictionary<string, string>
                                    {
                                        ["MyControlMessage"] = "True",
                                        [Headers.MessageId] = context.ControlMessageId
                                    },
                                    Array.Empty<byte>()
                                    ),
                                new UnicastAddressTag(context.DestinationQueueName)
                                )
                            );
                        var transportTransaction = new TransportTransaction();
                        return dispatcher.Dispatch(transportOperations, transportTransaction, new ContextBag());
                    }

                    protected override Task OnStop(IMessageSession session) => Task.CompletedTask;
                }
            }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>();
            }

            public class DoTheThing : Feature
            {
                public DoTheThing()
                {
                    EnableByDefault();
                }

                protected override void Setup(FeatureConfigurationContext context)
                {
                    context.Pipeline.Register<PipelineBehavior.Registration>();
                }

                class PipelineBehavior : Behavior<IIncomingPhysicalMessageContext>
                {
                    readonly MyContext myContext;

                    public PipelineBehavior(MyContext myContext)
                    {
                        this.myContext = myContext;
                    }

                    public override Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
                    {
                        if (context.MessageHeaders.ContainsKey("MyControlMessage"))
                        {
                            myContext.ControlMessageBodyLength = context.Message.Body.Length;
                            myContext.ControlMessageReceived = true;
                            return Task.CompletedTask;
                        }

                        return next();
                    }

                    public class Registration : RegisterStep
                    {
                        public Registration() : base("CatchControlMessage", typeof(PipelineBehavior), "Catch control message")
                        {
                        }
                    }
                }
            }
        }

        class MyContext : ScenarioContext
        {
            public string DestinationQueueName { get; set; }
            public string ControlMessageId { get; set; }
            public bool ControlMessageReceived { get; set; }
            public int ControlMessageBodyLength { get; set; }
        }
    }
}
