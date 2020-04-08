namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using McMaster.Extensions.CommandLineUtils;

    static class Endpoint
    {
        public static async Task Create(IAmazonSQS sqs, IAmazonSimpleNotificationService sns, IAmazonS3 s3, CommandArgument name, CommandOption largeMessageSupport, CommandOption delayDeliverySupport)
        {
            await Queue.Create(sqs, name, false);

            if(delayDeliverySupport.HasValue()) await Queue.Create(sqs, name, true);
            if (largeMessageSupport.HasValue()) await Bucket.Create(s3, name);
        }

        /* public static async Task Subscribe(ManagementClient client, CommandArgument name, CommandOption topicName, CommandOption subscriptionName, CommandArgument eventType, CommandOption ruleName)
         {
             try
             {
                 await Rule.Create(client, name, topicName, subscriptionName, eventType, ruleName);
             }
             catch (MessagingEntityAlreadyExistsException)
             {
                 Console.WriteLine($"Rule '{name}' for topic '{topicName}' and subscription '{subscriptionName}' already exists, skipping creation. Verify SQL filter matches '[NServiceBus.EnclosedMessageTypes] LIKE '%{eventType.Value}%'.");
             }
         }

         public static async Task Unsubscribe(ManagementClient client, CommandArgument name, CommandOption topicName, CommandOption subscriptionName, CommandArgument eventType, CommandOption ruleName)
         {
             try
             {
                 await Rule.Delete(client, name, topicName, subscriptionName, eventType, ruleName);
             }
             catch (MessagingEntityNotFoundException)
             {
                 Console.WriteLine($"Rule '{name}' for topic '{topicName}' and subscription '{subscriptionName}' does not exist, skipping deletion");
             }
         }*/
    }

}