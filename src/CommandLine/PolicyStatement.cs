namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;
    using System.Linq;
    using Amazon.Auth.AccessControlPolicy;
    using Amazon.Auth.AccessControlPolicy.ActionIdentifiers;

#pragma warning disable 618
    class PolicyStatement
    {
        public PolicyStatement(string topicName, string topicArn, string queueArn)
        {
            TopicName = topicName;
            TopicArn = topicArn;
            Statement = CreatePermissionStatement(queueArn, topicArn);
            QueueArn = queueArn;
        }

        public string QueueArn { get; }
        public string TopicName { get; }
        public string TopicArn { get; }
        public Statement Statement { get; }

        internal static Statement CreatePermissionStatement(string queueArn, string topicArn)
        {
            var statement = new Statement(Statement.StatementEffect.Allow);
            statement.Actions.Add(SQSActionIdentifiers.SendMessage);
            statement.Resources.Add(new Resource(queueArn));
            statement.Conditions.Add(ConditionFactory.NewSourceArnCondition(topicArn));
            statement.Principals.Add(new Principal("*"));
            return statement;
        }

        private static readonly string[] ArnSeperator = { ":" };
    }
#pragma warning restore 618
}