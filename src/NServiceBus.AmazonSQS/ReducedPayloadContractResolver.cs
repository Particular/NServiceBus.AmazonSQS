namespace NServiceBus.AmazonSQS
{
    using System.Reflection;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    class ReducedPayloadContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            property.ShouldSerialize = instance => property.DeclaringType != typeof(SqsTransportMessage) ||
                                                   (property.PropertyName != nameof(SqsTransportMessage.TimeToBeReceived) &&
                                                    property.PropertyName != nameof(SqsTransportMessage.ReplyToAddress));

            return property;
        }
    }
}