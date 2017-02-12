namespace NServiceBus.AmazonSQS.Extensions
{
    using System.Linq;
    using Amazon.S3.Model;

    public static class S3LifecycleConfigurationExtensions
    {
        public static bool ContainsMatchingRule(this LifecycleConfiguration lifeCycleConfiguration, LifecycleRule rule)
        {
            return lifeCycleConfiguration.Rules.Any(r => r.Id == rule.Id && r.Prefix == rule.Prefix && r.Status == rule.Status && r.Expiration.Matches(rule.Expiration));
        }
    }
}