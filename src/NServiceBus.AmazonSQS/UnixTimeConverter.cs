namespace NServiceBus.Transports.SQS
{
    using System;

    static class UnixTimeConverter
    {
#if NET452
        public static DateTimeOffset FromUnixTimeMilliseconds(long milliseconds)
        {
            return UnixEpoch.AddMilliseconds(milliseconds);
        }

        static readonly DateTimeOffset UnixEpoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
#else
    public static DateTimeOffset FromUnixTimeMilliseconds(long milliseconds) => DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
#endif
    }
}
