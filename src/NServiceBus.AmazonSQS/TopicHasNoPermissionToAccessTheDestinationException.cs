namespace NServiceBus
{
    using System;
    using System.Runtime.Serialization;
    using System.Security.Permissions;

    /// <summary>
    /// Exception that indicates the topic represented by the TopicArn has no permission to write into the destination of the EndpointArn.
    /// </summary>
    [Serializable]
    public sealed class TopicHasNoPermissionToAccessTheDestinationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of <see cref="TopicHasNoPermissionToAccessTheDestinationException" />.
        /// </summary>
        /// <param name="endpointArn">The ARN of the subscription endpoint.</param>
        /// <param name="topicArn">The ARN of the topic.</param>
        public TopicHasNoPermissionToAccessTheDestinationException(string endpointArn, string topicArn)
        {
            EndpointArn = endpointArn;
            TopicArn = topicArn;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="TopicHasNoPermissionToAccessTheDestinationException" />.
        /// </summary>
        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        TopicHasNoPermissionToAccessTheDestinationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            EndpointArn = info.GetString(nameof(EndpointArn));
            TopicArn = info.GetString(nameof(TopicArn));
        }

        /// <inheritdoc />
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            info.AddValue(nameof(EndpointArn), this.EndpointArn);
            info.AddValue(nameof(TopicArn), this.TopicArn);

            base.GetObjectData(info, context);
        }

        /// <summary>
        /// The endpoint ARN.
        /// </summary>
        public string EndpointArn { get; }

        /// <summary>
        /// The topic ARN.
        /// </summary>
        public string TopicArn { get; }
    }
}