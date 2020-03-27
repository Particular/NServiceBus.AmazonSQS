namespace NServiceBus.AmazonSQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Amazon.Runtime;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using NUnit.Framework;

    [TestFixture]
    public class Cleanup
    {
        [SetUp]
        public void SetUp()
        {
            var credentials = new EnvironmentVariablesAWSCredentials();
            sqsClient = new AmazonSQSClient(credentials);
            snsClient = new AmazonSimpleNotificationServiceClient(credentials);
        }

        [TearDown]
        public void Teardown()
        {
            sqsClient.Dispose();
            snsClient.Dispose();
        }

        [Test]
        [Explicit]
        public async Task DeleteAllQueuesUsedForTests()
        {
            await DeleteAllQueuesWithPrefix(sqsClient, "AT");
            await DeleteAllQueuesWithPrefix(sqsClient, "TT");
        }

        [Test]
        [Explicit]
        public async Task DeleteAllTopicsUsedForTests()
        {
            await DeleteAllTopicsWithPrefix(snsClient, "AT");
            await DeleteAllTopicsWithPrefix(snsClient, "TT");
        }

        public static async Task DeleteAllTopicsWithPrefix(IAmazonSimpleNotificationService snsClient, string topicNamePrefix)
        {
            ListTopicsResponse upToHundredTopics = null;
            do
            {
                upToHundredTopics = await snsClient.ListTopicsAsync(upToHundredTopics?.NextToken);
                var deletionTasks = new List<Task>(upToHundredTopics.Topics.Count);
                deletionTasks.AddRange(upToHundredTopics.Topics.Select(async topic =>
                {
                    if (!topic.TopicArn.Contains($":{topicNamePrefix}"))
                    {
                        return;
                    }

                    ListSubscriptionsByTopicResponse subscriptions = null; 
                    do
                    {
                        subscriptions = await snsClient.ListSubscriptionsByTopicAsync(topic.TopicArn, subscriptions?.NextToken).ConfigureAwait(false);
                        var subscriptionDeletionTasks = new List<Task>(subscriptions.Subscriptions.Count);
                        subscriptionDeletionTasks.AddRange(subscriptions.Subscriptions.Select(async subscription =>
                        {
                            try
                            {
                                await snsClient.UnsubscribeAsync(subscription.SubscriptionArn).ConfigureAwait(false);
                            }
                            catch (Exception)
                            {
                                Console.WriteLine($"Unable to delete subscription '{subscription.SubscriptionArn}' '{topic.TopicArn}'");
                            }
                        }));

                        await Task.WhenAll(subscriptionDeletionTasks).ConfigureAwait(false);
                    } while (subscriptions.NextToken != null && subscriptions.Subscriptions.Count > 0);

                    try
                    {
                        await snsClient.DeleteTopicAsync(topic.TopicArn).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"Unable to delete topic '{topic.TopicArn}'");
                    }
                }));
                await Task.WhenAll(deletionTasks).ConfigureAwait(false);
            } while (upToHundredTopics.NextToken != null && upToHundredTopics.Topics.Count > 0);
        }

        public static async Task DeleteAllQueuesWithPrefix(IAmazonSQS sqsClient, string queueNamePrefix)
        {
            ListQueuesResponse upToAThousandQueues;
            do
            {
                upToAThousandQueues = await sqsClient.ListQueuesAsync(queueNamePrefix);
                var deletionTasks = new List<Task>(upToAThousandQueues.QueueUrls.Count);
                deletionTasks.AddRange(upToAThousandQueues.QueueUrls.Select(async queueUrl =>
                {
                    try
                    {
                        await sqsClient.DeleteQueueAsync(queueUrl);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"Unable to delete queue '{queueUrl}'");
                    }
                }));
                await Task.WhenAll(deletionTasks).ConfigureAwait(false);
            } while (upToAThousandQueues.QueueUrls.Count > 0);
        }

        AmazonSQSClient sqsClient;
        AmazonSimpleNotificationServiceClient snsClient;
    }
}