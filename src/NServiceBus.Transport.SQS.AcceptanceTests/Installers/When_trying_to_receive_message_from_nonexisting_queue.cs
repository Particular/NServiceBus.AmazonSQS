namespace NServiceBus.AcceptanceTests.Installers
{
    using AcceptanceTesting;
    using Amazon.SQS.Model;
    using NServiceBus;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;

    public class When_trying_to_receive_message_from_nonexisting_queue : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_throw_exception_with_queue_name()
        {
            var exception = Assert.ThrowsAsync<QueueDoesNotExistException>(async () =>
            {
                await Scenario.Define<ScenarioContext>()
                    .WithEndpoint<Endpoint>()
                    .Done(context => context.EndpointsStarted)
                    .Run();
            });

            Assert.That(exception.Message, Contains.Substring("TryingToReceiveMessageFromNonexistingQueue"));
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint() =>
                EndpointSetup<DefaultServerWithNoInstallers>();
        }

        public class MyMessage : ICommand
        {
        }
    }
}