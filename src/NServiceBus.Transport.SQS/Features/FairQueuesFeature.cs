namespace NServiceBus.Transport.SQS.Features;

using System;
using System.Threading.Tasks;
using NServiceBus.Features;
using Pipeline;

class FairQueuesFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        var transportDefinition = context.Settings.Get<TransportDefinition>() as SqsTransport;

        if (transportDefinition?.DoNotAutomaticallyPropagateMessageGroupId ?? false)
        {
            return;
        }

        context.Pipeline.Register(new ApplyMessageGroupIdFromHeadersToOutgoingMessageBehavior(), "SQS apply MessageGroupId from headers to outgoing message behavior");
    }
}

class ApplyMessageGroupIdFromHeadersToOutgoingMessageBehavior : IBehavior<IOutgoingPhysicalMessageContext, IOutgoingPhysicalMessageContext>
{
    public Task Invoke(IOutgoingPhysicalMessageContext context, Func<IOutgoingPhysicalMessageContext, Task> next)
    {
        if (context.TryGetIncomingPhysicalMessage(out var incomingMessage)
            && incomingMessage.Headers.TryGetValue(TransportHeaders.MessageGroupId, out var messageGroupId)
            && !string.IsNullOrWhiteSpace(messageGroupId))
        {
            context.Headers[TransportHeaders.MessageGroupId] = messageGroupId;
        }
        return next(context);
    }
}