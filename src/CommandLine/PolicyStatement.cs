namespace NServiceBus.Transport.SQS.CommandLine
{
    using Amazon.Auth.AccessControlPolicy;
    using Amazon.Auth.AccessControlPolicy.ActionIdentifiers;

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

#pragma warning disable 618
        static Statement CreatePermissionStatement(string queueArn, string topicArn)
        {
            var statement = new Statement(Statement.StatementEffect.Allow);
            statement.Actions.Add(SQSActionIdentifiers.SendMessage);
            statement.Resources.Add(new Resource(queueArn));
            statement.Conditions.Add(ConditionFactory.NewSourceArnCondition(topicArn));
            statement.Principals.Add(new Principal("*"));
            return statement;
        }
#pragma warning restore 618
    }
}