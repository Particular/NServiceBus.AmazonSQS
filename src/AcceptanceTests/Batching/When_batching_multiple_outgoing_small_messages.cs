namespace NServiceBus.AcceptanceTests.Batching
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using EndpointTemplates;
    using Logging;
    using NUnit.Framework;

    public class When_batching_multiple_outgoing_small_messages : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_batch_in_packages_of_ten_for_non_immediate_dispatches()
        {
            var kickOffMessageId = Guid.NewGuid().ToString();

            var context = await Scenario.Define<Context>(c => c.LogLevel = LogLevel.Debug)
                .WithEndpoint<Sender>(b => b.When(session =>
                {
                    var options = new SendOptions();
                    options.RouteToThisEndpoint();
                    options.SetMessageId(kickOffMessageId);

                    return session.Send(new SendMessagesInBatches(), options);
                }))
                .WithEndpoint<Receiver>()
                .Done(c => c.ReceiveCount >= 60)
                .Run();

            foreach (var logItem in context.Logs)
            {
                if (logItem.Message.StartsWith("Sending batch") || logItem.Message.StartsWith("Sent batch"))
                {
                    Console.WriteLine(logItem.Message);
                }
            }

            Assert.AreEqual(60, context.ReceiveCount);
            // TODO add more asserts
        }

        public class Context : ScenarioContext
        {
            public int ReceiveCount;
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(builder =>
                {
                    builder.ConfigureTransport().Routing().RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
                });
            }

            public class BatchHandler : IHandleMessages<SendMessagesInBatches>
            {
                public async Task Handle(SendMessagesInBatches message, IMessageHandlerContext context)
                {
                    for (var i = 0; i < 50; i++)
                    {
                        await context.Send(new MyMessage());
                    }

                    for (var i = 0; i < 10; i++)
                    {
                        var options = new SendOptions();
                        options.RequireImmediateDispatch();
                        await context.Send(new MyMessage(), options);
                    }
                }
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Context Context { get; set; }

                public Task Handle(MyMessage messageWithLargePayload, IMessageHandlerContext context)
                {
                    Interlocked.Increment(ref Context.ReceiveCount);
                    return Task.FromResult(0);
                }
            }
        }

        public class SendMessagesInBatches : ICommand
        {
        }

        public class MyMessage : ICommand
        {
        }
    }
}