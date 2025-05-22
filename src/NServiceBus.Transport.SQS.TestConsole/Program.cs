// See https://aka.ms/new-console-template for more information

namespace NServiceBus.Transport.SQS.TestConsole;

class Program
{
    static async Task Main()
    {
        var transport = new SqsTransport();
        var hostSettings = new HostSettings("", "", new StartupDiagnosticEntries(), (s, exception, arg3) => { }, true);
        ReceiveSettings[] receivers = [new ReceiveSettings("", new QueueAddress(""), true, false, "")];
        string[] sendingAddresses = [""];
        var infra = await transport.Initialize(hostSettings, receivers, sendingAddresses).ConfigureAwait(false);
        await infra.Receivers[""].StartReceive().ConfigureAwait(false);
    }
}