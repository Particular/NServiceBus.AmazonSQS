namespace NServiceBus.SQS
{
    internal static class AddressExtensions
    {
        public static string ToSqsQueueName(this Address address)
        {
            return address.ToString().Replace('@', '-');
        }
    }
}
