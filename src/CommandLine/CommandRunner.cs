namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;

    static class CommandRunner
    {
     /*   public static async Task Run(CommandOption connectionString, Func<ManagementClient, Task> func)
        {
            var connectionStringToUse = connectionString.HasValue() ? connectionString.Value() : Environment.GetEnvironmentVariable(EnvironmentVariableName);

            var client = new ManagementClient(connectionStringToUse);

            await func(client);

            await client.CloseAsync();
        }*/

        public const string AccessKeyId = "AWS_ACCESS_KEY_ID";
        public const string Region = "AWS_REGION";
        public const string SecretAccessKey = "AWS_SECRET_ACCESS_KEY";
    }
}