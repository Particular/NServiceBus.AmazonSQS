namespace NServiceBus.AmazonSQS.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Pipeline;

    class TestIndependenceSkipBehavior : IBehavior<ITransportReceiveContext, ITransportReceiveContext>
    {
        readonly string testRunId;

        public TestIndependenceSkipBehavior(ScenarioContext scenarioContext)
        {
            testRunId = scenarioContext.TestRunId.ToString();
        }

        public Task Invoke(ITransportReceiveContext context, Func<ITransportReceiveContext, Task> next)
        {
            if (!context.Message.Headers.TryGetValue("$AcceptanceTesting.TestRunId", out var runId) || runId != testRunId)
            {
                Console.WriteLine($"Skipping message {context.Message.MessageId} from previous test run");
                return Task.FromResult(0);
            }

            return next(context);
        }
    }
}