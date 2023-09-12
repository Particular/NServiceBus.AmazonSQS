namespace NServiceBus.AcceptanceTests.Sending
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Features;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.Pipeline;
    using NServiceBus.Routing;
    using NUnit.Framework;
    using Transport;

    class When_sending_control_messages_without_body : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Can_be_sent_and_processed()
        {
            var context = await Scenario.Define<MyContext>(ctx =>
                {
                    ctx.DestinationQueueName = TestNameHelper.GetSqsQueueName("SendingControlMessagesWithoutBody.Receiver", SetupFixture.NamePrefix);
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
                EndpointSetup<DefaultServer>(cfg => cfg.ConfigureSqsTransport().DoNotWrapOutgoingMessages = true);
            }

            class DispatchControlMessageAtStartup : Feature
            {
                public DispatchControlMessageAtStartup()
                {
                    EnableByDefault();
                }

                protected override void Setup(FeatureConfigurationContext context)
                {
                    context.RegisterStartupTask(sp => new Startup(
                        sp.GetRequiredService<IMessageDispatcher>(),
                        sp.GetRequiredService<MyContext>())
                    );
                }

                class Startup : FeatureStartupTask
                {
                    readonly IMessageDispatcher dispatcher;
                    readonly MyContext context;

                    public Startup(IMessageDispatcher dispatcher, MyContext context)
                    {
                        this.dispatcher = dispatcher;
                        this.context = context;
                    }

                    protected override Task OnStart(IMessageSession session,
                        CancellationToken cancellationToken = new CancellationToken())
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
                        return dispatcher.Dispatch(transportOperations, transportTransaction, cancellationToken);
                    }

                    protected override Task OnStop(IMessageSession session,
                        CancellationToken cancellationToken = new CancellationToken()) => Task.CompletedTask;
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
