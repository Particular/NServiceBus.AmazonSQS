namespace NServiceBus.Transports.SQS
{
    // doesn't help to make this a struct since it gets boxed anyway by the extension bag
    class ValidateSubscriptionDestinationPolicies
    {
        ValidateSubscriptionDestinationPolicies()
        {
        }

        public static readonly ValidateSubscriptionDestinationPolicies Instance = new ValidateSubscriptionDestinationPolicies();
    }
}