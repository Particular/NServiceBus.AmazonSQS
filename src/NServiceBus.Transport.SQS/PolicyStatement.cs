namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Linq;
    using Amazon.Auth.AccessControlPolicy;

#pragma warning disable 618
    class PolicyStatement
    {
        public PolicyStatement(string topicName, string topicArn, Statement statement, string queueArn)
        {
            TopicName = topicName;
            TopicArn = topicArn;
            Statement = statement;
            QueueArn = queueArn;

            var splittedTopicArn = TopicArn.Split(ArnSeperator, StringSplitOptions.RemoveEmptyEntries);
            AccountArn = string.Join(":", splittedTopicArn.Take(5));
        }

        public string QueueArn { get; }

        public string TopicName { get; }
        public string TopicArn { get; }
        public string AccountArn { get; }
        public Statement Statement { get; }

        private static readonly string[] ArnSeperator = { ":" };
    }
#pragma warning restore 618
}