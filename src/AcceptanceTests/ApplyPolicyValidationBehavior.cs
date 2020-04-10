namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Pipeline;

    class ApplyPolicyValidationBehavior : Behavior<IOutgoingPublishContext>
    {
        public override Task Invoke(IOutgoingPublishContext context, Func<Task> next)
        {
            context.Extensions.RequireSubscriptionDestinationPolicyValidation();

            return next();
        }
    }
}