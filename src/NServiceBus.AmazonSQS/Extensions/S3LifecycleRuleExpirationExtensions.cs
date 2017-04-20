namespace NServiceBus.AmazonSQS.Extensions
{
    using Amazon.S3.Model;

    public static class S3LifecycleRuleExpirationExtensions
    {
        public static bool Matches(this LifecycleRuleExpiration expiration1, LifecycleRuleExpiration expiration2)
        {
            if (expiration1 == expiration2)
            {
                return true;
            }

            if (expiration1 == null || expiration2 == null)
            {
                return false;
            }

            return expiration1.Date == expiration2.Date && expiration1.Days == expiration2.Days;
        }
    }
}