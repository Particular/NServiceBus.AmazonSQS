namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// This converter is only used to make sure we are not double storing TimeToBeReceived and ReplyAddress both in
    /// the headers and on the transport message wrapper to save space on the SQS message body.
    /// The read method is implemented but never used because only the dispatcher uses it to serialize the transport
    /// message.
    /// </summary>
    sealed class ReducedPayloadSerializerConverter : JsonConverter<TransportMessage>
    {
        static readonly JsonSerializerOptions TransportMessageSerializerOptions = new()
        {
            TypeInfoResolver = TransportMessageSerializerContext.Default
        };

        static readonly JsonConverter<Dictionary<string, string>> DefaultDictionaryConverter =
            (JsonConverter<Dictionary<string, string>>)JsonSerializerOptions.Default.GetConverter(typeof(Dictionary<string, string>));
        public override TransportMessage Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options) =>
            JsonSerializer.Deserialize<TransportMessage>(ref reader, TransportMessageSerializerOptions);

        public override void Write(Utf8JsonWriter writer, TransportMessage value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString(nameof(TransportMessage.Body), value.Body);
            writer.WriteString(nameof(TransportMessage.S3BodyKey), value.S3BodyKey);

            writer.WritePropertyName(nameof(TransportMessage.Headers));

            DefaultDictionaryConverter.Write(writer, value.Headers, options);

            writer.WriteEndObject();
        }
    }
}