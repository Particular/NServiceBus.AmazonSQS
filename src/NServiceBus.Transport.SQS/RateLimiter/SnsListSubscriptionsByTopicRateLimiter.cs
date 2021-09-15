namespace NServiceBus.Transport.SQS
{
    using System;

    class SnsListSubscriptionsByTopicRateLimiter : RateLimiter
    {
        public SnsListSubscriptionsByTopicRateLimiter()
            : base(30, TimeSpan.FromSeconds(1), "ListSubscriptionsByTopic")
        {

        }
    }
}
