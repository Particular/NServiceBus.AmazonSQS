namespace NServiceBus.AcceptanceTests.Infrastructure
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Pipeline;

    class TestIndependenceStampBehavior : IBehavior<IDispatchContext, IDispatchContext>
    {
        string testRunId;

        public TestIndependenceStampBehavior(ScenarioContext scenarioContext)
        {
            testRunId = scenarioContext.TestRunId.ToString();
        }

        public Task Invoke(IDispatchContext context, Func<IDispatchContext, Task> next)
        {
            foreach (var o in context.Operations)
            {
                o.Message.Headers["$AcceptanceTesting.TestRunId"] = testRunId;
            }
            return next(context);
        }
    }
}