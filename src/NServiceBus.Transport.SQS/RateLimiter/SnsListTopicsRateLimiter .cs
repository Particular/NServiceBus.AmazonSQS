namespace NServiceBus.Transport.SQS
{
    using System;

    class SnsListTopicsRateLimiter : RateLimiter
    {
        public SnsListTopicsRateLimiter()
            : base(30, TimeSpan.FromSeconds(1), "ListTopics")
        {

        }
    }
}