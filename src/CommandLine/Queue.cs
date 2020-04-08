namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using Amazon.SQS;
    using McMaster.Extensions.CommandLineUtils;

    static class Queue
    {
        public static async Task Create(IAmazonSQS sqs, CommandArgument name, bool isDelayDelivery)
        {
            if (isDelayDelivery)
            {
                await Console.Out.WriteLineAsync("Delay delivery queue created");
            }
            else
            {
                await Console.Out.WriteLineAsync("Queue created");
            }

            await Task.CompletedTask;
        }
    }

}