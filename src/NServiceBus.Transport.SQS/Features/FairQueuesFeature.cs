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

        if (transportDefinition?.EnableFairQueues ?? false)
        {
            context.Pipeline.Register(new PersistIncomingMessageGroupIdToHeadersBehavior(), "SQS persist incoming MessageGroupId to headers behavior");
            context.Pipeline.Register(new ApplyMessageGroupIdFromHeadersToOutgoingMessageBehavior(), "SQS apply MessageGroupId from headers to outgoing message behavior");
        }
    }
}

class PersistIncomingMessageGroupIdToHeadersBehavior : IBehavior<IIncomingPhysicalMessageContext, IIncomingPhysicalMessageContext>
{
    public Task Invoke(IIncomingPhysicalMessageContext context, Func<IIncomingPhysicalMessageContext, Task> next)
    {
        if (context.Extensions.TryGet<Amazon.SQS.Model.Message>(out var nativeMessage)
            && nativeMessage.Attributes.TryGetValue("MessageGroupId", out var messageGroupId)
            && !string.IsNullOrWhiteSpace(messageGroupId))
        {
            context.Message.Headers[TransportHeaders.FairQueuesMessageGroupId] = messageGroupId;
        }
        return next(context);
    }
}

class ApplyMessageGroupIdFromHeadersToOutgoingMessageBehavior : IBehavior<IOutgoingPhysicalMessageContext, IOutgoingPhysicalMessageContext>
{
    public Task Invoke(IOutgoingPhysicalMessageContext context, Func<IOutgoingPhysicalMessageContext, Task> next)
    {
        if (context.TryGetIncomingPhysicalMessage(out var incomingMessage)
            && incomingMessage.Headers.TryGetValue(TransportHeaders.FairQueuesMessageGroupId, out var messageGroupId)
            && !string.IsNullOrWhiteSpace(messageGroupId))
        {
            context.Headers[TransportHeaders.FairQueuesMessageGroupId] = messageGroupId;
        }
        return next(context);
    }
}