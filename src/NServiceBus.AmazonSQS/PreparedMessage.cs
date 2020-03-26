namespace NServiceBus.Transports.SQS
{
    abstract class PreparedMessage
    {
        public abstract string MessageId { get; set; }
        public string Body { get; set; }
        public string Destination { get; set; }
        public long Size { get; private set; }

        public void CalculateSize()
        {
            Size = Body?.Length ?? 0;
            Size += CalculateAttributesSize();
        }

        protected abstract long CalculateAttributesSize();
    }
}