namespace NServiceBus
{
    using Extensibility;
    using Transports.SQS;

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
            options.GetExtensions().Set(ValidateSubscriptionDestinationPolicies.Instance);
        }
    }
}