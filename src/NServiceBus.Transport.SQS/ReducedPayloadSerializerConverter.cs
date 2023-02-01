namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    class ReducedPayloadSerializerConverter : JsonConverter<TransportMessage>
    {
        static readonly JsonConverter<TransportMessage> DefaultConverter =
            (JsonConverter<TransportMessage>)JsonSerializerOptions.Default.GetConverter(typeof(TransportMessage));

        static readonly JsonConverter<Dictionary<string, string>> DefaultDictionaryConverter =
            (JsonConverter<Dictionary<string, string>>)JsonSerializerOptions.Default.GetConverter(typeof(Dictionary<string, string>));
        public override TransportMessage Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options) =>
            DefaultConverter.Read(ref reader, typeToConvert, options);

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