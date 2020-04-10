namespace NServiceBus.AmazonSQS.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Pipeline;

    class TestIndependenceBehavior : IBehavior<IOutgoingPhysicalMessageContext, IOutgoingPhysicalMessageContext>
    {
        public TestIndependenceBehavior(ScenarioContext scenarioContext)
        {
            testRunId = scenarioContext.TestRunId.ToString();
        }

        public Task Invoke(IOutgoingPhysicalMessageContext context, Func<IOutgoingPhysicalMessageContext, Task> next)
        {
            context.Headers["$AcceptanceTesting.TestRunId"] = testRunId;
            return next(context);
        }

        readonly string testRunId;
    }
}