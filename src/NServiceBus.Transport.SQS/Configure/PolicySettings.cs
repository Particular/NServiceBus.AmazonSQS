namespace NServiceBus
{
    using System;
    using System.Linq;
    using System.Collections.Generic;

    /// <summary>
    /// Exposes the settings to configure policy creation
    /// </summary>
    public partial class PolicySettings
    {
        bool setupTopicPoliciesWhenSubscribing = true;
        bool accountCondition;
        bool topicNamePrefixCondition;

        /// <summary>
        /// Controls if the transport sets up the IAM policies for topics to allow them to send messages to the input queue.
        /// </summary>
        public bool SetupTopicPoliciesWhenSubscribing
        {
            get => setupTopicPoliciesWhenSubscribing;
            set
            {
                if (AccountCondition || TopicNamePrefixCondition || TopicNamespaceConditions.Any())
                {
                    throw new Exception("Cannot disable policy setup if policy creation has been configured.");
                }
                setupTopicPoliciesWhenSubscribing = value;
            }
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
        public bool AccountCondition
        {
            get => accountCondition;
            set
            {
                if (!setupTopicPoliciesWhenSubscribing)
                {
                    throw new Exception("Cannot configure policy creation if policy setup has been disabled.");
                }
                accountCondition = value;
            }
        }


        /// <summary>
        /// Adds a topic name prefix wildcard condition.
        /// </summary>
        /// <example>
        /// <code>
        ///    transport.TopicNamePrefix = "DEV-"
        /// </code> and subscribing to
        /// - arn:aws:sns:some-region:some-account:DEV-Some-Namespace-Event
        /// - arn:aws:sns:some-region:some-account:DEV-Some-Namespace-AnotherEvent
        /// would lead to
        /// <![CDATA[
        /// "Condition" : { "ArnLike" : { "aws:SourceArn" : "arn:aws:sns:some-region:some-account:DEV-*" } }
        /// ]]>
        /// </example>
        /// <remarks>Calling this method will opt-in for wildcard policy and no longer populate the policy with the explicit topic ARNs the endpoint subscribes to.</remarks>
        public bool TopicNamePrefixCondition
        {
            get => topicNamePrefixCondition;
            set
            {
                if (!setupTopicPoliciesWhenSubscribing)
                {
                    throw new Exception("Cannot configure policy creation if policy setup has been disabled.");
                }
                topicNamePrefixCondition = value;
            }
        }

        /// <summary>
        /// Adds one or multiple topic namespace wildcard conditions.
        /// </summary>
        /// <example>
        /// <code>
        ///    var policies = transport.Policies;
        ///    policies.TopicNamespaceConditions.Add("Some.Namespace.");
        ///    policies.TopicNamespaceConditions.Add("SomeOther.Namespace");
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
        /// <remarks>It is possible to use dots in the provided namespace (for example Sales.VipCustomers.). The namespaces will be translated into a compliant format.</remarks>
        /// <remarks>Calling this method will opt-in for wildcard policy and no longer populate the policy with the explicit topic ARNs the endpoint subscribes to.</remarks>
        public List<string> TopicNamespaceConditions { get; } = new List<string>();
    }
}