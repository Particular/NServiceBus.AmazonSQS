
namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Text;

    static class TopicNameHelper
    {
        public static string GetSnsTopicName(Type eventType, string topicNamePrefix)
        {
            var destination = eventType.FullName;

            var s = topicNamePrefix + destination;
            if (s.Length > 256)
            {
                throw new Exception($"Address {destination} with configured prefix {topicNamePrefix} is longer than 256 characters and therefore cannot be used to create an SNS topic. Use a shorter topic name.");
            }

            var topicNameBuilder = new StringBuilder(s);

            return GetSanitizedTopicName(topicNameBuilder);
        }

        public static string GetSanitizedTopicName(StringBuilder topicNameBuilder)
        {
            // SNS topic names can only have alphanumeric characters, hyphens and underscores.
            // Any other characters will be replaced with a hyphen.
            for (var i = 0; i < topicNameBuilder.Length; ++i)
            {
                var c = topicNameBuilder[i];
                if (!char.IsLetterOrDigit(c)
                    && c != '-'
                    && c != '_')
                {
                    topicNameBuilder[i] = '-';
                }
            }

            return topicNameBuilder.ToString();
        }
    }
}