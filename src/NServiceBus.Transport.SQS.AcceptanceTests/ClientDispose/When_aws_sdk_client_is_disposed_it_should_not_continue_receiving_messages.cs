namespace NServiceBus.AcceptanceTests.ClientDispose
{
    using NUnit.Framework;
    using System.Threading.Tasks;
    using Amazon.SQS.Model;
    using Amazon.SQS;

    class When_aws_sdk_client_is_disposed_it_should_not_continue_receiving_messages
    {
        [Test]
        public async Task Should_not_receive_message_after_dispose()
        {
            System.Diagnostics.Debug.WriteLine($"Waiting 60 seconds so purges can be done");
            await Task.Delay(60000);
            System.Diagnostics.Debug.WriteLine($"Creating sender client");
            var sendSqsClient = ConfigureEndpointSqsTransport.CreateSqsClient();
            System.Diagnostics.Debug.WriteLine($"Creating sender queue");
            await sendSqsClient.CreateQueueAsync("travis_send_client");
            var sendSqsQueueUrl = await sendSqsClient.GetQueueUrlAsync("travis_send_client");
            System.Diagnostics.Debug.WriteLine($"Sender queue created: {sendSqsQueueUrl.QueueUrl}");
            System.Diagnostics.Debug.WriteLine($"Purging sender queue");
            await sendSqsClient.PurgeQueueAsync(sendSqsQueueUrl.QueueUrl);

            GetQueueUrlResponse receiveSqsQueueUrl = null;
            for (int i = 0; i < 20; i++)
            {
                System.Diagnostics.Debug.WriteLine($"Creating receiver client");
                IAmazonSQS receiveSqsclient = ConfigureEndpointSqsTransport.CreateSqsClient();

                await receiveSqsclient.CreateQueueAsync("travis_recieve_client");
                receiveSqsQueueUrl = await sendSqsClient.GetQueueUrlAsync("travis_recieve_client");
                System.Diagnostics.Debug.WriteLine($"Queue created: {receiveSqsQueueUrl.QueueUrl}");

                // Clean the queue on the first creation since this operation can only be run once every 60 seconds
                if (i == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Purging receiver queue");
                    await receiveSqsclient.PurgeQueueAsync(receiveSqsQueueUrl.QueueUrl);
                }

                var messageRequest = new ReceiveMessageRequest
                {
                    QueueUrl = receiveSqsQueueUrl.QueueUrl,
                    VisibilityTimeout = 300
                };

                System.Diagnostics.Debug.WriteLine($"Getting messages from receiver queue");
                var response = await receiveSqsclient.ReceiveMessageAsync(messageRequest);

                if (response.Messages.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine("I found some messages");
                }

                //foreach (var message in response.Messages)
                //{
                //    var delRequest = new DeleteMessageRequest
                //    {
                //        QueueUrl = receiveSqsQueueUrl.QueueUrl,
                //        ReceiptHandle = message.ReceiptHandle
                //    };

                //    await receiveSqsclient.DeleteMessageAsync(delRequest);
                //}

                receiveSqsclient.Dispose();
                System.Diagnostics.Debug.WriteLine("Receiver client disposed");
            }
            System.Diagnostics.Debug.WriteLine($"Sending messages to: {receiveSqsQueueUrl.QueueUrl}");
            await sendSqsClient.SendMessageAsync(new SendMessageRequest(receiveSqsQueueUrl.QueueUrl, "Hello World"));
            System.Diagnostics.Debug.WriteLine($"Message sent");
            System.Diagnostics.Debug.WriteLine("Waiting 61 seconds");
            await Task.Delay(61000);
        }
    }
}
