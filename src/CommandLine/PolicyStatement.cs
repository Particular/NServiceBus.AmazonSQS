namespace NServiceBus.Transport.SQS.CommandLine;

using Amazon.Auth.AccessControlPolicy;

class PolicyStatement(string topicName, string topicArn, string queueArn)
{
    public string QueueArn { get; } = queueArn;
    public string TopicName { get; } = topicName;
    public string TopicArn { get; } = topicArn;
    public Statement Statement { get; } = CreatePermissionStatement(queueArn, topicArn);

#pragma warning disable 618
    static Statement CreatePermissionStatement(string queueArn, string topicArn)
    {
        var statement = new Statement(Statement.StatementEffect.Allow);
        statement.Actions.Add(new ActionIdentifier("sqs:SendMessage"));
        statement.Resources.Add(new Resource(queueArn));
        statement.Conditions.Add(ConditionFactory.NewSourceArnCondition(topicArn));
        statement.Principals.Add(new Principal("*"));
        return statement;
    }
#pragma warning restore 618
}