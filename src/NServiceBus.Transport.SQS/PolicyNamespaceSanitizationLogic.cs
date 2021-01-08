namespace NServiceBus.Transport.SQS
{
    using System.Text;

    static class PolicyNamespaceSanitizationLogic
    {
        public static string GetNamespaceName(StringBuilder namespaceNameBuilder)
        {
            // SNS topic names can only have alphanumeric characters, hyphens and underscores.
            // Any other characters will be replaced with a hyphen.
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

            return namespaceNameBuilder.ToString();
        }
    }
}