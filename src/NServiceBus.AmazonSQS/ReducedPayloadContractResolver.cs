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

            property.ShouldSerialize = instance => property.DeclaringType != typeof(TransportMessage) || 
                                                   (property.PropertyName != nameof(TransportMessage.TimeToBeReceived) && 
                                                    property.PropertyName != nameof(TransportMessage.ReplyToAddress));

            return property;
        }
    }
}
