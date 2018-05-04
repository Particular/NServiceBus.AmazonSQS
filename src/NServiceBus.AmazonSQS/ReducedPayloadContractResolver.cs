namespace NServiceBus.AmazonSQS
{
    using System.Collections.Generic;
    using System.Reflection;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;
    class ReducedPayloadContractResolver : DefaultContractResolver
    {
        static IList<string> PropertiesToIgnore = new []
        {
            nameof(TransportMessage.TimeToBeReceived),
            nameof(TransportMessage.ReplyToAddress)
        };

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            property.ShouldSerialize = instance => property.DeclaringType != typeof(TransportMessage) || !PropertiesToIgnore.Contains(property.PropertyName);

            return property;
        }
    }
}
