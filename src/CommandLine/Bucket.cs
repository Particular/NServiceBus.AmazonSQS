namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using Amazon.S3;
    using McMaster.Extensions.CommandLineUtils;

    static class Bucket
    {
        public static async Task Create(IAmazonS3 s3, CommandArgument name)
        {
            await Console.Out.WriteLineAsync("Delay delivery queue created");

            await Task.CompletedTask;
        }
    }

}