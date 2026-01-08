namespace NServiceBus.Transport.SQS.Tests;

class TransportTestsConstraints
{
    public const int SqsHeadersBuffer = 500;
    public const int SqsMaximumMessageSizeKb = TransportConstraints.SqsMaximumMessageSize / 1024;
    public const int SqsMessageIdBuffer = 4;
}