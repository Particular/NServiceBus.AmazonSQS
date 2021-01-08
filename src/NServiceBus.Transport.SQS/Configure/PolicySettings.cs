namespace NServiceBus
{
    using Settings;
    using System.Collections.Generic;
    using Configuration.AdvancedExtensibility;
    using Transport.SQS.Configure;

    /// <summary>
    /// Exposes the settings to configure policy creation
    /// </summary>
    public class PolicySettings : ExposeSettings
    {
        internal PolicySettings(SettingsHolder settings, bool forceSettlement = false) : base(settings)
        {
            this.GetSettings().Set(SettingsKeys.ForceSettlementForPolicies, forceSettlement);
        }

        /// <summary>
        ///
        /// </summary>
        public void AddAccountCondition()
        {
            this.GetSettings().Set(SettingsKeys.AddAccountConditionForPolicies, true);
        }

        /// <summary>
        ///
        /// </summary>
        public void AddTopicNamePrefixCondition()
        {
            this.GetSettings().Set(SettingsKeys.AddTopicNamePrefixConditionForPolicies, true);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="topicNamespace">The namespace of the topic.</param>
        /// <remarks>It is possible to use dots in the provided namespace. The namespaces will be translated into a compliant format.</remarks>
        public void AddNamespaceCondition(string topicNamespace)
        {
            if (!this.GetSettings()
                .TryGet<List<string>>(SettingsKeys.NamespaceConditionForPolicies, out var topicNamespaces))
            {
                topicNamespaces = new List<string>();
                this.GetSettings().Set(SettingsKeys.NamespaceConditionForPolicies, topicNamespaces);
            }
            topicNamespaces.Add(topicNamespace);
        }
    }

    // policy.AddAccountCondition();
    // policy.AddTopicNamePrefixCondition(); // extracted from transport.TopicNamePrefix("DEV-")
    // policy.AddNamespaceCondition("Sales."); // dots turned to dashes and if prefix set it would be taken into account
    // policy.AddNamespaceCondition("Shipping."); // dots turned to dashes and if prefix set it would be taken into account
    // default we use TopicArn, if any of the Add*Conditions are called we no longer add the full topic arns
}