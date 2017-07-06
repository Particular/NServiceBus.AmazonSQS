namespace NServiceBus.AcceptanceTests.Infrastructure
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Pipeline;
    using NServiceBus.Transport;

    class TestIndependenceSkipBehavior : IBehavior<ITransportReceiveContext, ITransportReceiveContext>
    {
        string testRunId;

        public TestIndependenceSkipBehavior(ScenarioContext scenarioContext)
        {
            testRunId = scenarioContext.TestRunId.ToString();
        }

        public Task Invoke(ITransportReceiveContext context, Func<ITransportReceiveContext, Task> next)
        {
            if (context.Message.GetMesssageIntent() == MessageIntentEnum.Subscribe || context.Message.GetMesssageIntent() == MessageIntentEnum.Unsubscribe)
                return next(context);

            string runId;
            
            if (!context.Message.Headers.TryGetValue("$AcceptanceTesting.TestRunId", out runId) || runId != testRunId)
            {
                Console.WriteLine($"Skipping message {context.Message.MessageId} because its TestRunId {runId} does not match the current TestRunId {testRunId}");
                return Task.CompletedTask;
            }

            return next(context);
        }
    }
}