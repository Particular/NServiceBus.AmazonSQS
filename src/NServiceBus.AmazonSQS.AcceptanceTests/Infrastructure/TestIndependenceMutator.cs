namespace NServiceBus.AcceptanceTests.Infrastructure
{
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.MessageMutator;

    class TestIndependenceMutator : IMutateOutgoingTransportMessages
    {
        string testRunId;

        public TestIndependenceMutator(ScenarioContext scenarioContext)
        {
            testRunId = scenarioContext.TestRunId.ToString();
        }

        public Task MutateOutgoing(MutateOutgoingTransportMessageContext context)
        {
            context.OutgoingHeaders["$AcceptanceTesting.TestRunId"] = testRunId;
            return Task.CompletedTask;
        }
    }
}