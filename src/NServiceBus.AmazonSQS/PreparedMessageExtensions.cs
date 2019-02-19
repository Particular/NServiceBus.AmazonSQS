namespace NServiceBus.Transports.SQS
{
    using System;
    using Amazon.SQS.Model;

    static class PreparedMessageExtensions
    {
        public static SendMessageRequest ToRequest(this PreparedMessage message)
        {
            return new SendMessageRequest(message.QueueUrl, message.Body)
            {
                MessageGroupId = message.MessageGroupId,
                MessageDeduplicationId = message.MessageDeduplicationId,
                MessageAttributes = message.MessageAttributes,
                DelaySeconds = Convert.ToInt32(message.DelaySeconds)
            };
        }
    }
}