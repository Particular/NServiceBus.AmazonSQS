namespace NServiceBus.Transport.AmazonSQS
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