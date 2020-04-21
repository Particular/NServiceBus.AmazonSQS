namespace NServiceBus.Transport.SQS.CommandLine.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using NUnit.Framework;

    [SetUpFixture]
    public class Cleanup
    {   

        public static Task DeleteAllResourcesWithPrefix(IAmazonSQS sqsClient, IAmazonSimpleNotificationService snsClient, IAmazonS3 s3Client, string namePrefix)
        {
            return Task.WhenAll(
                DeleteAllQueuesWithPrefix(sqsClient, namePrefix), 
                DeleteAllTopicsWithPrefix(snsClient, namePrefix), 
                DeleteAllSubscriptionsWithPrefix(snsClient, namePrefix),
                DeleteAllBucketsWithPrefix(s3Client, namePrefix));
        }

        public static async Task DeleteAllSubscriptionsWithPrefix(IAmazonSimpleNotificationService snsClient, string topicNamePrefix)
        {
            var deletedSubscriptionArns = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                ListSubscriptionsResponse subscriptions;
                do
                {
                    subscriptions = await snsClient.ListSubscriptionsAsync();

                    // if everything returned here has already been deleted it is probably a good time to stop trying due to the eventual consistency
                    if (subscriptions.Subscriptions.All(subscription => deletedSubscriptionArns.Contains(subscription.SubscriptionArn)))
                    {
                        return;
                    }

                    var deletionTasks = new List<Task>(subscriptions.Subscriptions.Count);
                    deletionTasks.AddRange(subscriptions.Subscriptions
                        .Where(subscription => !deletedSubscriptionArns.Contains(subscription.SubscriptionArn))
                        .Select(async subscription =>
                        {
                            if (!subscription.TopicArn.Contains($":{topicNamePrefix}"))
                            {
                                return;
                            }

                            try
                            {
                                await snsClient.UnsubscribeAsync(subscription.SubscriptionArn).ConfigureAwait(false);
                                deletedSubscriptionArns.Add(subscription.SubscriptionArn);
                            }
                            catch (Exception)
                            {
                                Console.WriteLine($"Unable to delete subscription '{subscription.SubscriptionArn}' '{subscription.TopicArn}'");
                            }
                        }));

                    await Task.WhenAll(deletionTasks).ConfigureAwait(false);
                } while (subscriptions.NextToken != null && subscriptions.Subscriptions.Count > 0);
            }
            catch (Exception)
            {
                Console.WriteLine($"Unable to delete subscriptions with topic prefix '{topicNamePrefix}'");
            }
        }

        public static async Task DeleteAllTopicsWithPrefix(IAmazonSimpleNotificationService snsClient, string topicNamePrefix)
        {
            var deletedTopicArns = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                ListTopicsResponse upToHundredTopics = null;
                do
                {
                    upToHundredTopics = await snsClient.ListTopicsAsync(upToHundredTopics?.NextToken);

                    // if everything returned here has already been deleted it is probably a good time to stop trying due to the eventual consistency
                    if (upToHundredTopics.Topics.All(topic => deletedTopicArns.Contains(topic.TopicArn)))
                    {
                        return;
                    }

                    var deletionTasks = new List<Task>(upToHundredTopics.Topics.Count);
                    deletionTasks.AddRange(upToHundredTopics.Topics
                        .Where(topic => !deletedTopicArns.Contains(topic.TopicArn))
                        .Select(async topic =>
                        {
                            if (!topic.TopicArn.Contains($":{topicNamePrefix}"))
                            {
                                return;
                            }

                            try
                            {
                                await snsClient.DeleteTopicAsync(topic.TopicArn).ConfigureAwait(false);
                                deletedTopicArns.Add(topic.TopicArn);
                            }
                            catch (Exception)
                            {
                                Console.WriteLine($"Unable to delete topic '{topic.TopicArn}'");
                            }
                        }));
                    await Task.WhenAll(deletionTasks).ConfigureAwait(false);
                } while (upToHundredTopics.NextToken != null && upToHundredTopics.Topics.Count > 0);
            }
            catch (Exception)
            {
                Console.WriteLine($"Unable to delete topics with prefix '{topicNamePrefix}'");
            }
        }

        public static async Task DeleteAllQueuesWithPrefix(IAmazonSQS sqsClient, string queueNamePrefix)
        {
            var deletedQueueUrls = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                ListQueuesResponse upToAThousandQueues;
                do
                {
                    upToAThousandQueues = await sqsClient.ListQueuesAsync(queueNamePrefix);
                    // if everything returned here has already been deleted it is probably a good time to stop trying due to the eventual consistency
                    if (upToAThousandQueues.QueueUrls.All(url => deletedQueueUrls.Contains(url)))
                    {
                        return;
                    }

                    var deletionTasks = new List<Task>(upToAThousandQueues.QueueUrls.Count);
                    deletionTasks.AddRange(upToAThousandQueues.QueueUrls
                        .Where(url => !deletedQueueUrls.Contains(url))
                        .Select(async queueUrl =>
                        {
                            try
                            {
                                await sqsClient.DeleteQueueAsync(queueUrl);
                                deletedQueueUrls.Add(queueUrl);
                            }
                            catch (Exception)
                            {
                                Console.WriteLine($"Unable to delete queue '{queueUrl}'");
                            }
                        }));
                    await Task.WhenAll(deletionTasks).ConfigureAwait(false);
                } while (upToAThousandQueues.QueueUrls.Count > 0);
            }
            catch (Exception)
            {
                Console.WriteLine($"Unable to delete queues with prefix '{queueNamePrefix}'");
            }
        }

        public static async Task DeleteAllBucketsWithPrefix(IAmazonS3 s3Client, string bucketNamePrefix)
        {
            var deletedBucketNames = new HashSet<string>(StringComparer.Ordinal);
            try
            {

                var listBucketsResponse = await s3Client.ListBucketsAsync(new ListBucketsRequest()).ConfigureAwait(false);
                var buckets = listBucketsResponse.Buckets.Where(x => x.BucketName.StartsWith(bucketNamePrefix, StringComparison.OrdinalIgnoreCase)).Select(b => b.BucketName);
                foreach(var bucketName in buckets)
                {
                    try
                    {
                        await s3Client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = bucketName }).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"Unable to delete bucket with name '{bucketName}'");
                    }
                }

            }
            catch (Exception)
            {
                Console.WriteLine($"Unable to delete buckets with prefix '{bucketNamePrefix}'");
            }
        }
    }

}