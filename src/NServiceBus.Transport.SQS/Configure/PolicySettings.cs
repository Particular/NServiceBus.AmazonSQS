namespace NServiceBus
{
    using System;
    using System.Linq;
    using Settings;
    using System.Collections.Generic;
    using Configuration.AdvancedExtensibility;
    using Transport.SQS.Configure;

    /// <summary>
    /// Exposes the settings to configure policy creation
    /// </summary>
    public class PolicySettings : ExposeSettings
    {
        internal PolicySettings(SettingsHolder settings) : base(settings)
        {
        }

        /// <summary>
        /// Disables policy updates by assuming the endpoint input queue has the necessary IAM policy that allows all topics subscribed to send messages
        /// </summary>
        public void AssumePolicyHasAppropriatePermissions()
        {
            var settings = this.GetSettings();
            if (settings.HasExplicitValue(SettingsKeys.AddAccountConditionForPolicies) &&
                settings.Get<bool>(SettingsKeys.AddAccountConditionForPolicies) ||
                settings.HasExplicitValue(SettingsKeys.AddTopicNamePrefixConditionForPolicies) &&
                settings.Get<bool>(SettingsKeys.AddTopicNamePrefixConditionForPolicies) ||
                settings.HasExplicitValue(SettingsKeys.NamespaceConditionForPolicies) &&
                settings.Get<List<string>>(SettingsKeys.NamespaceConditionForPolicies).Any() )
            {
                throw new InvalidOperationException(
                    $"When the policy modification is disabled no other condition like `{nameof(AddAccountCondition)}`, `{nameof(AddTopicNamePrefixCondition)}` or `{nameof(AddNamespaceCondition)}` can be used.");
            }

            this.GetSettings().Set(SettingsKeys.AssumePolicyHasAppropriatePermissions, true);
        }

        /// <summary>
        /// Adds an account wildcard condition for every account found on the topics subscribed to.
        /// </summary>
        /// <example>
        /// Subscribing to
        /// - arn:aws:sns:some-region:some-account:Some-Namespace-Event
        /// - arn:aws:sns:some-region:some-account:Some-Namespace-AnotherEvent
        /// would lead to
        /// <![CDATA[
        /// "Condition" : { "ArnLike" : { "aws:SourceArn" : "arn:aws:sns:some-region:some-account:*" } }
        /// ]]>
        /// </example>
        /// <remarks>Calling this method will opt-in for wildcard policy and no longer populate the policy with the explicit topic ARNs the endpoint subscribes to.</remarks>
        public void AddAccountCondition()
        {
            var settings = this.GetSettings();
            if (settings.HasExplicitValue(SettingsKeys.AssumePolicyHasAppropriatePermissions) &&
                settings.Get<bool>(SettingsKeys.AssumePolicyHasAppropriatePermissions))
            {
                throw new InvalidOperationException(
                    $"The policy modification was disabled by calling `{nameof(AssumePolicyHasAppropriatePermissions)}`. `{nameof(AddAccountCondition)}` requires `{nameof(AssumePolicyHasAppropriatePermissions)}` to be removed.");
            }

            this.GetSettings().Set(SettingsKeys.AddAccountConditionForPolicies, true);
        }

        /// <summary>
        /// Adds a topic name prefix wildcard condition.
        /// </summary>
        /// <example>
        /// <code>
        ///    transport.TopicNamePrefix("DEV-")
        /// </code> and subscribing to
        /// - arn:aws:sns:some-region:some-account:DEV-Some-Namespace-Event
        /// - arn:aws:sns:some-region:some-account:DEV-Some-Namespace-AnotherEvent
        /// would lead to
        /// <![CDATA[
        /// "Condition" : { "ArnLike" : { "aws:SourceArn" : "arn:aws:sns:some-region:some-account:DEV-*" } }
        /// ]]>
        /// </example>
        /// <remarks>Calling this method will opt-in for wildcard policy and no longer populate the policy with the explicit topic ARNs the endpoint subscribes to.</remarks>
        public void AddTopicNamePrefixCondition()
        {
            var settings = this.GetSettings();
            if (settings.HasExplicitValue(SettingsKeys.AssumePolicyHasAppropriatePermissions) &&
                settings.Get<bool>(SettingsKeys.AssumePolicyHasAppropriatePermissions))
            {
                throw new InvalidOperationException(
                    $"The policy modification was disabled by calling `{nameof(AssumePolicyHasAppropriatePermissions)}`. `{nameof(AddTopicNamePrefixCondition)}` requires `{nameof(AssumePolicyHasAppropriatePermissions)}` to be removed.");
            }

            this.GetSettings().Set(SettingsKeys.AddTopicNamePrefixConditionForPolicies, true);
        }

        /// <summary>
        /// Adds one or multiple topic namespace wildcard conditions.
        /// </summary>
        /// <example>
        /// <code>
        ///    var policies = transport.Policies();
        ///    policies.AddNamespaceCondition("Some.Namespace.");
        ///    policies.AddNamespaceCondition("SomeOther.Namespace");
        /// </code> and subscribing to
        /// - arn:aws:sns:some-region:some-account:Some-Namespace-Event
        /// - arn:aws:sns:some-region:some-account:Some-Namespace-AnotherEvent
        /// would lead to
        /// <![CDATA[
        /// "Condition" : { "ArnLike" : { "aws:SourceArn" :
        ///    "arn:aws:sns:some-region:some-account:Some-Namespace-*",
        ///    "arn:aws:sns:some-region:some-account:SomeOther-Namespace*",
        /// } }
        /// ]]>
        /// </example>
        /// <param name="topicNamespace">The namespace of the topic.</param>
        /// <remarks>It is possible to use dots in the provided namespace (for example Sales.VipCustomers.). The namespaces will be translated into a compliant format.</remarks>
        /// <remarks>Calling this method will opt-in for wildcard policy and no longer populate the policy with the explicit topic ARNs the endpoint subscribes to.</remarks>
        public void AddNamespaceCondition(string topicNamespace)
        {
            var settings = this.GetSettings();
            if (settings.HasExplicitValue(SettingsKeys.AssumePolicyHasAppropriatePermissions) &&
                settings.Get<bool>(SettingsKeys.AssumePolicyHasAppropriatePermissions))
            {
                throw new InvalidOperationException(
                    $"The policy modification was disabled by calling `{nameof(AssumePolicyHasAppropriatePermissions)}`. `{nameof(AddNamespaceCondition)}` requires `{nameof(AssumePolicyHasAppropriatePermissions)}` to be removed.");
            }

            if (!this.GetSettings()
                .TryGet<List<string>>(SettingsKeys.NamespaceConditionForPolicies, out var topicNamespaces))
            {
                topicNamespaces = new List<string>();
                this.GetSettings().Set(SettingsKeys.NamespaceConditionForPolicies, topicNamespaces);
            }
            topicNamespaces.Add(topicNamespace);
        }
    }
}