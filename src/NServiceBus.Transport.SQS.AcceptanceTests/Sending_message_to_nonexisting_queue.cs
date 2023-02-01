namespace NServiceBus.AcceptanceTests
{
    using System;
    using AcceptanceTesting;
    using Amazon.SQS.Model;
    using EndpointTemplates;
    using NUnit.Framework;

    public class Sending_message_to_nonexisting_queue : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_throw_exception_with_queue_name()
        {
            var destination = "myfakequeue";
            var messageId = Guid.NewGuid();
            var exception = Assert.ThrowsAsync<QueueDoesNotExistException>(async () =>
            {
                var result = await Scenario.Define<Context>(c =>
                {
                    c.MessageId = messageId;
                })
                    .WithEndpoint<Endpoint>(b => b
                        .When(session => session.Send(destination, new MyMessage())))
                        .Done(context => true)
                    .Run();
            });

            Assert.IsTrue(exception.Message.Contains(destination));
        }

        public class Context : ScenarioContext
        {
            public Guid MessageId { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>();
            }
        }

        public class MyMessage : ICommand
        {
        }
    }
}