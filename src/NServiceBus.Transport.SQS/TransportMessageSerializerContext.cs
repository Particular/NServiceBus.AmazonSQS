namespace NServiceBus.Transport.SQS
{
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions]
    [JsonSerializable(typeof(TransportMessage))]
    partial class TransportMessageSerializerContext : JsonSerializerContext
    {
    }
}