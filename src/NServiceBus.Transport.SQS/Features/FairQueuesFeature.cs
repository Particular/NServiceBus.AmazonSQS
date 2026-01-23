namespace NServiceBus.Transport.SQS.Features;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus.Features;
using Pipeline;

class FairQueuesFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        context.Pipeline.Register(new PersistIncomingMessageGroupIdToHeadersBehavior(), "SQS persist incoming MessageGroupId to headers behavior");
        context.Pipeline.Register(new ApplyMessageGroupIdFromHeadersToOutgoingMessageBehavior(), "SQS apply MessageGroupId from headers to outgoing message behavior");
    }
}

class PersistIncomingMessageGroupIdToHeadersBehavior : Behavior<IIncomingPhysicalMessageContext>
{
    public override async Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
    {
        if (context.Extensions.TryGet<Amazon.SQS.Model.Message>(out var nativeMessage))
        {
            var messageGroupId = nativeMessage.Attributes.GetValueOrDefault("MessageGroupId");
            if (!string.IsNullOrWhiteSpace(messageGroupId))
            {
                context.Message.Headers[TransportHeaders.FairQueuesMessageGroupId] = messageGroupId;
            }
        }
        await next().ConfigureAwait(false);
    }
}

class ApplyMessageGroupIdFromHeadersToOutgoingMessageBehavior : Behavior<IOutgoingPhysicalMessageContext>
{
    public override async Task Invoke(IOutgoingPhysicalMessageContext context, Func<Task> next)
    {
        if (context.TryGetIncomingPhysicalMessage(out var incomingMessage))
        {
            if (incomingMessage.Headers.TryGetValue(TransportHeaders.FairQueuesMessageGroupId, out var messageGroupId))
            {
                if (!string.IsNullOrWhiteSpace(messageGroupId))
                {
                    context.Headers[TransportHeaders.FairQueuesMessageGroupId] = messageGroupId;
                }
            }
        }
        await next().ConfigureAwait(false);
    }
}