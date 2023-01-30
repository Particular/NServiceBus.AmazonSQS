# NServiceBus.AmazonSQS

This is an Amazon SQS transport for NServiceBus.

Feel free to browse and contribute!

For more information, including a guide on getting started quickly, see the project documentation at [docs.particular.net](https://docs.particular.net/transports/sqs/).

## Running the Acceptance Tests

The solution contains the [NServiceBus Acceptance Test suite](https://www.nuget.org/packages/NServiceBus.AcceptanceTests.Sources/) and the [NServiceBus Transport Test suite](https://www.nuget.org/packages/NServiceBus.TransportTests.Sources/).
To run the tests, the Access Key ID and Secret Access Key of an AWS IAM account need to be set in environment variables on the machine running the tests. Full details on how to set this up can be found [here](https://docs.particular.net/transports/sqs/#getting-started-set-up-an-aws-account).

The transport can be configured using the following environment variables:

* **NSERVICEBUS_AMAZONSQS_S3BUCKET** corresponds to the [S3BucketForLargeMessages](https://docs.particular.net/transports/sqs/configuration-options#s3bucketforlargemessages) parameter. Default is no S3 bucket.

The bucket should not have encryption enabled. An additional bucket `{NSERVICEBUS_AMAZONSQS_S3BUCKET}.kms` with AWS KMS encryption enabled is required.

Additional environment variables required for AWS:

 * **AWS_ACCESS_KEY_ID** access key ID to sign programmatic requests that you make to AWS. Provisioned via IAM.
 * **AWS_SECRET_ACCESS_KEY** secret access key to sign programmatic requests that you make to AWS. Provisioned via IAM.
 * **AWS_REGION** Valid AWS region.

## AWS Permissions

In addition to the [permissions required to run the transport](https://docs.particular.net/transports/sqs/#prerequisites), running the tests also requires:

* sns:ListSubscriptionsByTopic
* sns:CreateTopic
* sns:Subscribe
* sns:Publish
* sqs:DeleteQueue
* sns:DeleteTopic
* s3:DeleteBucket
* sqs:DeleteQueue
* sns:DeleteTopic

## Queue Names in Acceptance Tests

The names of queues used by the acceptance tests take the following form:

    AT<unique-prefix>-<pre-truncated-queue-name>

Where

 * `AT` stands for "Acceptance Test"
 * `unique-prefix` is an identifier that uniquely identifies a single test run so that parallel test runs do not conflict.
 * `pre-truncated-queue-name` is the name of the queue, "pre-truncated" (characters are removed from the beginning) so that the entire queue name is 80 characters or less.

This scheme accomplishes the following goals:

 * Test runs are idempotent - each test run uses its own set of queues
 * Queues for a given test run are easily searchable by prefix in the SQS portal
 * The discriminator and qualifier at the end of the queue name are not interfered with
 * Queue names fit the 80 character limit imposed by SQS

## Cleanup scheduled task

This repo has a [GitHub action](/actions/workflows/tests-cleanup.yml) that deletes stale AWS objects created when the tests run. It takes care of deleting S3 buckets older than 24 hours with the cli- prefix in the name. The same GitHub action code can be updated to delete any other AWS object created by the tests that fail to be deleted during the tests cleanup phase.

The cleanup workflow requires the same permissions as running the tests.
