// See https://aka.ms/new-console-template for more information

namespace NServiceBus.Transport.SQS.TestConsole;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Microsoft.Extensions.Configuration;

class Program
{
    static async Task Main()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var queueName = "orderplacedevent";
        var sqsClient = new AmazonSQSClient(new BasicAWSCredentials(config["Aws:AccessKey"], config["Aws:SecretAccessKey"]), RegionEndpoint.GetBySystemName(config["Aws:Region"]));
        var snsClient = new AmazonSimpleNotificationServiceClient(new BasicAWSCredentials(config["Aws:AccessKey"], config["Aws:SecretAccessKey"]), RegionEndpoint.GetBySystemName(config["Aws:Region"]));
        var transport = new SqsTransport(sqsClient, snsClient, true);
        var hostSettings = new HostSettings(queueName, $"SQS Test Console for {queueName}", new StartupDiagnosticEntries(), (_, _, _) => { }, true, null);
        ReceiveSettings[] receivers = [new ReceiveSettings(queueName, new QueueAddress(queueName), false, false, $"{queueName}_error")];
        string[] sendingAddresses = [queueName];

        var cts = new CancellationTokenSource();

        var infra = await transport.Initialize(hostSettings, receivers, sendingAddresses, cts.Token).ConfigureAwait(false);
        var receiver = infra.Receivers[queueName];
        await receiver.Initialize(new PushRuntimeSettings(), OnMessage, OnError, cancellationToken: cts.Token).ConfigureAwait(false);
        await receiver.StartReceive(cts.Token).ConfigureAwait(false);


        Console.ReadKey();

        await cts.CancelAsync().ConfigureAwait(false);

        Console.ReadKey();
    }

    static Task<ErrorHandleResult> OnError(ErrorContext errorcontext, CancellationToken cancellationtoken)
    {
        Console.WriteLine(errorcontext.Exception.Message);
        return Task.FromResult(ErrorHandleResult.Handled);
    }

    static Task OnMessage(MessageContext messagecontext, CancellationToken cancellationtoken)
    {
        var body = JsonSerializer.Deserialize<JsonNode>(Encoding.UTF8.GetString(messagecontext.Body.Span));
        Console.WriteLine($"\n----------Received message----------" +
                          $"\nHeaders:\n{JsonSerializer.Serialize(messagecontext.Headers, new JsonSerializerOptions { WriteIndented = true })}" +
                          $"\nBody:\n{JsonSerializer.Serialize(body, new JsonSerializerOptions { WriteIndented = true })}");
        return Task.CompletedTask;
    }
}