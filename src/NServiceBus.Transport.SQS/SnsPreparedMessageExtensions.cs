namespace NServiceBus.Transport.SQS
{
    using System.Linq;
    using Amazon.SimpleNotificationService.Model;
    using SnsMessageAttributeValue = Amazon.SimpleNotificationService.Model.MessageAttributeValue;

    static class SnsPreparedMessageExtensions
    {
        public static PublishRequest ToPublishRequest(this SnsPreparedMessage message) =>
            new(message.Destination, message.Body)
            {
                MessageAttributes = message.MessageAttributes.ToDictionary(x => x.Key, x => new SnsMessageAttributeValue
                {
                    DataType = x.Value.DataType,
                    StringValue = x.Value.StringValue,
                    BinaryValue = x.Value.BinaryValue,
                })
            };
    }
}