namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Pipeline;
    using Transports.SQS;

    class ApplyPolicyValidationBehavior : Behavior<IOutgoingPublishContext>
    {
        public override Task Invoke(IOutgoingPublishContext context, Func<Task> next)
        {
            context.Extensions.Set(ValidateSubscriptionDestinationPolicies.Instance);

            return next();
        }
    }
}