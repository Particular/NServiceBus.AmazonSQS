namespace NServiceBus
{
    using Extensibility;
    using Transport.AmazonSQS;

    /// <summary>
    /// PublishOptions extensions from the transport
    /// </summary>
    public static class SqsTransportPublishOptionsExtensions
    {
        /// <summary>
        /// Enables subscription destination validation.
        /// </summary>
        public static void RequireSubscriptionDestinationPolicyValidation(this PublishOptions options)
        {
            options.GetExtensions().RequireSubscriptionDestinationPolicyValidation();
        }

        /// <summary>
        /// Enables subscription destination validation.
        /// </summary>
        public static void RequireSubscriptionDestinationPolicyValidation(this ContextBag options)
        {
            options.Set(ValidateSubscriptionDestinationPolicies.Instance);
        }
    }
}