namespace NServiceBus
{
    using Routing;
    using Settings;
    using Transport;
    using Transports.SQS;

    public class SqsTransport : TransportDefinition, IMessageDrivenSubscriptionTransport
    {
        public override string ExampleConnectionStringForErrorMessage
         => "Region=ap-southeast-2;S3BucketForLargeMessages=myBucketName;S3KeyPrefix=my/key/prefix;";

        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            return new SqsTransportInfrastructure(settings, connectionString);
        }
    }
}
