namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;

    public static class PolicyScrubber
    {
        public static string ScrubPolicy(string policyAsString)
        {
            var scrubbed = Regex.Replace(policyAsString, "\"Sid\" : \"(.*)\",", string.Empty);
            return RemoveUnnecessaryWhiteSpace(scrubbed);
        }

        private static string RemoveUnnecessaryWhiteSpace(string policyAsString)
        {
            return string.Join(Environment.NewLine, policyAsString.Split(new[]
                {
                    Environment.NewLine
                }, StringSplitOptions.RemoveEmptyEntries)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.TrimEnd())
            );
        }
    }
}