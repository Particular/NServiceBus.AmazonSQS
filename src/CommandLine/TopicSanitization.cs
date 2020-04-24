namespace NServiceBus.Transport.SQS.CommandLine
{
    using System.Text;

    public class TopicSanitization
    {
        public static string GetSanitizedTopicName(string topicName)
        {
            var topicNameBuilder = new StringBuilder(topicName);
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