namespace NServiceBus
{
    using Settings;
    using Configuration.AdvancedExtensibility;

    /// <summary>
    /// Exposes the settings to configure policy creation
    /// </summary>
    public class PolicySettings : ExposeSettings
    {
        internal PolicySettings(SettingsHolder settings, bool forceSettlement = false) : base(settings)
        {
        }

        /// <summary>
        ///
        /// </summary>
        public void AddAccountCondition()
        {

        }

        /// <summary>
        ///
        /// </summary>
        public void AddTopicNamePrefixCondition()
        {

        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="topicNamespace">The namespace of the topic.</param>
        /// <remarks>It is possible to use dots in the provided namespace. The namespaces will be translated into a compliant format.</remarks>
        public void AddNamespaceCondition(string topicNamespace)
        {

        }
    }

    // policy.AddAccountCondition();
    // policy.AddTopicNamePrefixCondition(); // extracted from transport.TopicNamePrefix("DEV-")
    // policy.AddNamespaceCondition("Sales."); // dots turned to dashes and if prefix set it would be taken into account
    // policy.AddNamespaceCondition("Shipping."); // dots turned to dashes and if prefix set it would be taken into account
    // default we use TopicArn, if any of the Add*Conditions are called we no longer add the full topic arns
}