NServiceBus.AmazonSQS
===============

![toucan](https://raw.githubusercontent.com/ahofman/NServiceBus.AmazonSQS/master/toucan-large.png)

[![Build status](https://ci.appveyor.com/api/projects/status/p4yb15sa17kq89gs/branch/master?svg=true)](https://ci.appveyor.com/project/ahofman/nservicebus-amazonsqs/branch/master)

This is an Amazon SQS transport for NServiceBus V5 and V6.

Feel free to browse and contribute!

For more information, including a guide on getting started quickly, see the project documentation at [docs.particular.net](https://docs.particular.net/transports/sqs/).

Running the Acceptance Tests
===============

The solution contains the [NServiceBus Acceptance Test suite](https://www.nuget.org/packages/NServiceBus.AcceptanceTests.Sources/) and the [NServiceBus Transport Test suite](https://www.nuget.org/packages/NServiceBus.TransportTests.Sources/).
To run the tests, the Access Key ID and Secret Access Key of an AWS IAM account need to be set in environment variables on the machine running the tests. Full details on how to set this up can be found [here](https://docs.particular.net/transports/sqs/#getting-started-set-up-an-aws-account).

The transport can be configured using the following environment variables:

* **NServiceBus.AmazonSQS.Region** corresponds to the [Region](https://docs.particular.net/transports/sqs/configuration-options#region) parameter. Default is "ap-southeast-2".
* **NServiceBus.AmazonSQS.S3Bucket** corresponds to the [S3BucketForLargeMessages](https://docs.particular.net/transports/sqs/configuration-options#s3bucketforlargemessages) parameter. Default is no S3 bucket.
* **NServiceBus.AmazonSQS.NativeDeferral** corresponds to the [NativeDeferral](https://docs.particular.net/transports/sqs/configuration-options#nativedeferral) parameter. Default is false.

### Queue Names in Acceptance Tests

The names of queues used by the acceptance tests take the following form:

    AT<datetime>-<pre-truncated-queue-name>
    
Where

* `AT` stands for "Acceptance Test"
* `datetime` is a date and time as yyyyMMddHHmmss that uniquely identifies a single test run. For example, when 100 tests are executed in a single test run each queue will have the same datetime timestamp.
* `pre-truncated-queue-name` is the name of the queue, "pre-truncated" (characters are removed from the beginning) so that the entire queue name is 80 characters or less. 

This scheme accomplishes the following goals:

* Test runs are idempotent - each test run uses its own set of queues
* Queues for a given test run are easily searchable by prefix in the SQS portal
* The discriminator and qualifier at the end of the queue name are not interfered with 
* Queue names fit the 80 character limit imposed by SQS

Project Icon
===============
Toucan by Creative Stall from the Noun Project
