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
#pragma warning disable IDE0004 // Remove Unnecessary Cast
                args: new object[] { (Func<Type, bool>)IsMessageType },
#pragma warning restore IDE0004 // Remove Unnecessary Cast
                culture: CultureInfo.InvariantCulture);
            settings.Set(messageMetadataRegistry);
            return messageMetadataRegistry;
        }
    }
}