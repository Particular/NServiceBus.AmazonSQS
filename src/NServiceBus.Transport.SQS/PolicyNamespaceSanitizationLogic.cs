namespace NServiceBus.Transport.SQS
{
    using System.Text;

    static class PolicyNamespaceSanitizationLogic
    {
        public static string GetNamespaceName(string topicNamePrefix, string namespaceName)
        {
            // SNS topic names can only have alphanumeric characters, hyphens and underscores.
            // Any other characters will be replaced with a hyphen.
            var namespaceNameBuilder = new StringBuilder(namespaceName);
            for (var i = 0; i < namespaceNameBuilder.Length; ++i)
            {
                var c = namespaceNameBuilder[i];
                if (!char.IsLetterOrDigit(c)
                    && c != '-'
                    && c != '_')
                {
                    namespaceNameBuilder[i] = '-';
                }
            }

            // topicNamePrefix should not be sanitized
            return topicNamePrefix + namespaceNameBuilder;
        }
    }
}