// ReSharper disable once UnusedParameter.Global
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace NServiceBus
{
    using System;

    public static partial class SqsTransportSettings
    {
        [ObsoleteEx(
            ReplacementTypeOrMember = nameof(MaxTimeToLive),
            RemoveInVersion = "5.0",
            TreatAsErrorFromVersion = "4.0")]
        public static TransportExtensions<SqsTransport> MaxTTLDays(this TransportExtensions<SqsTransport> transportExtensions, int maxTtlDays)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "transport.ClientFactory(() => new AmazonSqsClient(customConfig))",
            RemoveInVersion = "5.0",
            TreatAsErrorFromVersion = "4.0")]
        public static TransportExtensions<SqsTransport> CredentialSource(this TransportExtensions<SqsTransport> transportExtensions, SqsCredentialSource credentialSource)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "transport.ClientFactory(() => new AmazonSQSClient(new AmazonSQSConfig { RegionEndpoint = region }))",
            RemoveInVersion = "5.0",
            TreatAsErrorFromVersion = "4.0")]
        public static TransportExtensions<SqsTransport> Region(this TransportExtensions<SqsTransport> transportExtensions, string region)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "transport.ClientFactory(() => new AmazonSQSClient(new AmazonSQSConfig { ProxyHost = host, ProxyPort = port }))",
            RemoveInVersion = "5.0",
            TreatAsErrorFromVersion = "4.0")]
        public static TransportExtensions<SqsTransport> Proxy(this TransportExtensions<SqsTransport> transportExtensions, string proxyHost, int proxyPort)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "transport.S3(string bucketForLargeMessages, string keyPrefix)",
            RemoveInVersion = "5.0",
            TreatAsErrorFromVersion = "4.0")]
        public static TransportExtensions<SqsTransport> S3BucketForLargeMessages(this TransportExtensions<SqsTransport> transportExtensions, string s3BucketForLargeMessages, string s3KeyPrefix)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(Message = "This API is no longer needed because SQS is always used to delay messages.",
            RemoveInVersion = "5.0",
            TreatAsErrorFromVersion = "4.0")]
        public static TransportExtensions<SqsTransport> NativeDeferral(this TransportExtensions<SqsTransport> transportExtensions, bool use = true)
        {
            throw new NotImplementedException();
        }
    }

    [ObsoleteEx(
        ReplacementTypeOrMember = "transport.ClientFactory(() => new AmazonSqsClient(customConfig))",
        RemoveInVersion = "5.0",
        TreatAsErrorFromVersion = "4.0")]
    public enum SqsCredentialSource
    {
        EnvironmentVariables,
        InstanceProfile
    }
}