namespace NServiceBus.AcceptanceTests.Infrastructure
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Pipeline;

    class TestIndependenceStampBehavior : IBehavior<IOutgoingPhysicalMessageContext, IOutgoingPhysicalMessageContext>
    {
        string testRunId;

        public TestIndependenceStampBehavior(ScenarioContext scenarioContext)
        {
            testRunId = scenarioContext.TestRunId.ToString();
        }

        public Task Invoke(IOutgoingPhysicalMessageContext context, Func<IOutgoingPhysicalMessageContext, Task> next)
        {
            context.Headers["$AcceptanceTesting.TestRunId"] = testRunId;
            return next(context);
        }
    }
}