namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Globalization;
    using System.Reflection;
    using Settings;
    using Unicast.Messages;

    public static class SettingsHolderExtensions
    {
        public static MessageMetadataRegistry SetupMessageMetadataRegistry(this SettingsHolder settings)
        {
            bool IsMessageType(Type t) => true;
            var messageMetadataRegistry = (MessageMetadataRegistry)Activator.CreateInstance(
                type: typeof(MessageMetadataRegistry),
                bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                args: new object[] { (Func<Type, bool>)IsMessageType },
                culture: CultureInfo.InvariantCulture);
            settings.Set(messageMetadataRegistry);
            return messageMetadataRegistry;
        }
    }
}