namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading.Tasks;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using McMaster.Extensions.CommandLineUtils;

    static class Queue
    {
        public static async Task Create(IAmazonSQS sqs, CommandArgument name)
        {
            var endpointName = name.Value;
            var sqsRequest = new CreateQueueRequest { QueueName = endpointName };
            await Console.Out.WriteLineAsync($"Creating SQS Queue with name '{sqsRequest.QueueName}' for endpoint '{endpointName}'.");
            var createQueueResponse = await sqs.CreateQueueAsync(sqsRequest).ConfigureAwait(false);
            var sqsAttributesRequest = new SetQueueAttributesRequest { QueueUrl = createQueueResponse.QueueUrl };
            sqsAttributesRequest.Attributes.Add(QueueAttributeName.MessageRetentionPeriod, DefaultConfigurationValues.MaxTimeToLive.TotalSeconds.ToString(CultureInfo.InvariantCulture));            
            await sqs.SetQueueAttributesAsync(sqsAttributesRequest).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Created SQS Queue with name '{sqsRequest.QueueName}' for endpoint '{endpointName}'.");
        }

        public static async Task CreateDelayDelivery(IAmazonSQS sqs, CommandArgument name)
        {
            var endpointName = name.Value;            
            var delayedDeliveryQueueName = $"{endpointName}{DefaultConfigurationValues.DelayedDeliveryQueueSuffix}";
            var sqsRequest = new CreateQueueRequest
            {
                QueueName = delayedDeliveryQueueName,
                Attributes = new Dictionary<string, string> { { "FifoQueue", "true" } }
            };
            await Console.Out.WriteLineAsync($"Creating SQS delayed delivery queue with name '{sqsRequest.QueueName}' for endpoint '{endpointName}'.");
            var createQueueResponse = await sqs.CreateQueueAsync(sqsRequest).ConfigureAwait(false);
            var sqsAttributesRequest = new SetQueueAttributesRequest { QueueUrl = createQueueResponse.QueueUrl };
            sqsAttributesRequest.Attributes.Add(QueueAttributeName.MessageRetentionPeriod, DefaultConfigurationValues.DelayedDeliveryQueueMessageRetentionPeriod.TotalSeconds.ToString(CultureInfo.InvariantCulture));
            sqsAttributesRequest.Attributes.Add(QueueAttributeName.DelaySeconds, DefaultConfigurationValues.DelayedDeliveryQueueDelayTime.ToString(CultureInfo.InvariantCulture));
            await sqs.SetQueueAttributesAsync(sqsAttributesRequest).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Created SQS delayed delivery queue with name '{sqsRequest.QueueName}' for endpoint '{endpointName}'.");
        }
    }

}