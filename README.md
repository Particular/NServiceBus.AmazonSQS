NServiceBus.SQS
===============

This is an SQS transport for NServiceBus. It currently in the early stages of development and is not suitable for production use at this point in time. However, if you'd like to try a pre-release version, follow the below steps!

Feel free to browse and contribute!

### Set Up An AWS Account
You will need an [AWS IAM](http://docs.aws.amazon.com/IAM/latest/UserGuide/IAM_Introduction.html) account with a pair of [Access Keys](http://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSGettingStartedGuide/AWSCredentials.html) to use NServiceBus.SQS. 
The account needs the following permissions:
* SQS::CreateQueue
* SQS::DeleteMessage
* SQS::GetQueueUrl
* SQS::ReceiveMessage
* SQS::SendMessage
* S3::PutBucket
* S3::DeleteObject
* S3::GetObject
* S3::PutObject
* S3::PutLifecycleConfiguration

Once you have a pair of Access Keys (Access Key ID and Secret Access Key), you will need to store them in environment variables of the machine that is running your endpoint:
* Access Key ID goes in AWS_ACCESS_KEY_ID 
* Secret Access Key goes in AWS_SECRET_ACCESS_KEY

### Install The NuGet Package

There are only pre-release versions of the NuGet package available at this point in time, so you must supply the `-Pre` flag when installing the pacakge.

    PM> Install-Package NServiceBus.SQS -Pre

See the [NuGet Gallery](https://www.nuget.org/packages/NServiceBus.SQS) for details. 

### Configure Your Endpoint
When self-hosting:

    busConfiguration
        .UseTransport<NServiceBus.SqsTransport>()

When hosting in the NServiceBus.Host: 

    public class EndpointConfig : 
        IConfigureThisEndpoint, 
        AsA_Server, // Or AsA_Client, whatever
        UsingTransport<NServiceBus.SqsTransport>

Add a connection string to your app.config (or web.config):

    <connectionStrings>
        <add name="NServiceBus/Transport" connectionString="Region=ap-southeast-2;S3BucketForLargeMessages=myBucketName;S3KeyPrefix=my/key/prefix;" />
    </connectionStrings>

And you should be good to go!

For more documentation on what you can pass to the connection string, check out [this page](https://github.com/ahofman/NServiceBus.SQS/wiki/Configuration-Options).
