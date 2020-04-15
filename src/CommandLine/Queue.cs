namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading.Tasks;
    using Amazon.SQS;
    using Amazon.SQS.Model;

    static class Queue
    {
        public static async Task<string> GetUrl(IAmazonSQS sqs, string prefix, string endpointName)
        {
            var queueName = $"{prefix}{endpointName}";
            var getQueueUrlRequest = new GetQueueUrlRequest(queueName);
            var queueUrlResponse = await sqs.GetQueueUrlAsync(getQueueUrlRequest).ConfigureAwait(false);
            return queueUrlResponse.QueueUrl;
        }

        public static async Task<string> GetArn(IAmazonSQS sqs, string prefix, string endpointName)
        {
            var queueUrl = await GetUrl(sqs, prefix, endpointName);
            var queueAttributesResponse = await sqs.GetQueueAttributesAsync(queueUrl, new List<string> { "QueueArn" }).ConfigureAwait(false);
            return queueAttributesResponse.QueueARN;
        }

        public static async Task<string> Create(IAmazonSQS sqs, string prefix, string endpointName, double retentionPeriodInSeconds)
        {
            var queueName = $"{prefix}{endpointName}";
            var sqsRequest = new CreateQueueRequest { QueueName = queueName };
            await Console.Out.WriteLineAsync($"Creating SQS Queue with name '{sqsRequest.QueueName}' for endpoint '{endpointName}'.");
            var createQueueResponse = await sqs.CreateQueueAsync(sqsRequest).ConfigureAwait(false);
            var sqsAttributesRequest = new SetQueueAttributesRequest { QueueUrl = createQueueResponse.QueueUrl };
            sqsAttributesRequest.Attributes.Add(QueueAttributeName.MessageRetentionPeriod, retentionPeriodInSeconds.ToString(CultureInfo.InvariantCulture));            
            await sqs.SetQueueAttributesAsync(sqsAttributesRequest).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Created SQS Queue with name '{sqsRequest.QueueName}' for endpoint '{endpointName}'.");
            return createQueueResponse.QueueUrl;
        }

        public static async Task<string> CreateDelayDelivery(IAmazonSQS sqs, string prefix, string endpointName, double delayInSeconds, double retentionPeriodInSeconds, string suffix)
        {
            var delayedDeliveryQueueName = $"{prefix}{endpointName}{suffix}";
            var sqsRequest = new CreateQueueRequest
            {
                QueueName = delayedDeliveryQueueName,
                Attributes = new Dictionary<string, string> { { "FifoQueue", "true" } }
            };
            await Console.Out.WriteLineAsync($"Creating SQS delayed delivery queue with name '{sqsRequest.QueueName}' for endpoint '{endpointName}'.");
            var createQueueResponse = await sqs.CreateQueueAsync(sqsRequest).ConfigureAwait(false);
            var sqsAttributesRequest = new SetQueueAttributesRequest { QueueUrl = createQueueResponse.QueueUrl };
            sqsAttributesRequest.Attributes.Add(QueueAttributeName.MessageRetentionPeriod, retentionPeriodInSeconds.ToString(CultureInfo.InvariantCulture));
            sqsAttributesRequest.Attributes.Add(QueueAttributeName.DelaySeconds, delayInSeconds.ToString(CultureInfo.InvariantCulture));
            await sqs.SetQueueAttributesAsync(sqsAttributesRequest).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Created SQS delayed delivery queue with name '{sqsRequest.QueueName}' for endpoint '{endpointName}'.");
            return createQueueResponse.QueueUrl;
        }

        public static async Task Delete(IAmazonSQS sqs, string prefix, string endpointName)
        {
            var queueName = $"{prefix}{endpointName}";
            var getQueueUrlRequest = new GetQueueUrlRequest(queueName);
            var queueUrlResponse = await sqs.GetQueueUrlAsync(getQueueUrlRequest).ConfigureAwait(false);
            var deleteRequest = new DeleteQueueRequest { QueueUrl = queueUrlResponse.QueueUrl };
            await Console.Out.WriteLineAsync($"Deleting SQS Queue with url '{deleteRequest.QueueUrl}' for endpoint '{endpointName}'.");
            var deleteQueueResponse = await sqs.DeleteQueueAsync(deleteRequest).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Deleted SQS Queue with url '{deleteRequest.QueueUrl}' for endpoint '{endpointName}'.");
        }

        public static async Task DeleteDelayDelivery(IAmazonSQS sqs, string prefix, string endpointName, string suffix)
        {
            var delayedDeliveryQueueName = $"{prefix}{endpointName}{suffix}";
            var getQueueUrlRequest = new GetQueueUrlRequest(delayedDeliveryQueueName);
            var queueUrlResponse = await sqs.GetQueueUrlAsync(getQueueUrlRequest).ConfigureAwait(false);
            var deleteRequest = new DeleteQueueRequest { QueueUrl = queueUrlResponse.QueueUrl };
            await Console.Out.WriteLineAsync($"Deleting SQS delayed delivery queue with url '{deleteRequest.QueueUrl}' for endpoint '{endpointName}'.");
            var deleteQueueResponse = await sqs.DeleteQueueAsync(deleteRequest).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Deleted SQS delayed delivery queue with url '{deleteRequest.QueueUrl}' for endpoint '{endpointName}'.");
        }
    }
}