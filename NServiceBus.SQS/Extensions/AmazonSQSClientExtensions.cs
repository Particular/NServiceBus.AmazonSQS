using System.Collections.Generic;
using System.Linq;
using Amazon.SQS;
using Amazon.SQS.Model;
using Newtonsoft.Json;
using NServiceBus.Unicast;

namespace NServiceBus.SQS
{
    internal static class AmazonSQSClientExtensions
    {
        public static void SendTransportMessage(this IAmazonSQS sqs, TransportMessage message, SendOptions sendOptions)
        {
            var getQueueUrlRequest = new GetQueueUrlRequest(sendOptions.Destination.ToSqsQueueName());
            var getQueueUrlResponse = sqs.GetQueueUrl(getQueueUrlRequest);

            SendMessageRequest sendMessageRequest = new SendMessageRequest(getQueueUrlResponse.QueueUrl, "");

            sendMessageRequest.MessageBody = JsonConvert.SerializeObject(new SqsTransportMessage(message));
            sqs.SendMessage(sendMessageRequest);
        }

        public static Message DequeueMessage(this IAmazonSQS sqs, string queueUrl)
        {
            var receiveMessageRequest = new ReceiveMessageRequest
            {
                WaitTimeSeconds = 20,
                QueueUrl = queueUrl,
                MaxNumberOfMessages = 1,
                MessageAttributeNames = new List<string> { "All" }
            };

            var receiveMessageResponse = sqs.ReceiveMessage(receiveMessageRequest);
            return receiveMessageResponse.Messages.FirstOrDefault();
        }
    }
}
