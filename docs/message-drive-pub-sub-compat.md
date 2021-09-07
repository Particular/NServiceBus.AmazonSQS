## Publish-subscribe compatibility mode

Up until version 4, SQS Transport implemented publish-subscribe messaging using Core's message-driven feature. This changed in version 5, in which publish-subscribe has been based on AWS SNS.

In order to provide a migration path for a system with a mixture of endpoints using message-driven pub-sub and SNS-based pub-sub a [compatibility mode](https://docs.particular.net/transports/sqs/configuration-options#message-driven-pubsub-compatibility-mode) is provided in version 5 of the transport. Compatibility mode enables newer endpoints to act as publishers both for native subscribers and SNS subscribers.

When running in compatibility mode, a publisher will publish events using both SNS (events passed by Core as multicast messages) and message-driven pub-sub (events passed by Core as unicast messages). This ensures that events get delivered both to older and newer endpoints. 

However in a scenario, when a subscriber upgrades from message-driven to SNS-based pub-sub this leads to duplicated event delivery. In order to prevent that, a publisher running in compatibility mode performs message deduplication before any events get sent. First, messages passed to the transport are grouped by `messageId`. If there is any `messageId` for which a multicast and some unicast messages have been found the deduplication process is performed. In such a case, SNS is queried to get all subscribers for the multicast message topic and all unicast messages with the exact same destination already subscribed to the topic are dropped.

## Constraints

The de-duplication mechanism requires information on the SNS subscribers which is queried by the transport using AWS API. The API in turn comes with built-in throttling of [30 req/sec](https://docs.aws.amazon.com/sns/latest/api/API_ListSubscriptionsByTopic.html).

The initial version of the compatibility mode assumed that these limits will never be reached in production systems which turned out to be [wrong](https://github.com/Particular/NServiceBus.AmazonSQS/issues/866). As a result, enhancements have been introduced to prevent over extensive SNS querring in high-throughput scenarios:

* Information on topics and topic subscriptions are cached for a configurable amount of time.
* When an entry is not found in the cache only a single request to SNS is performed and all other send operations wait for the result.
* Requests to the SNS client are rate limited to a maximum of 30 per second. Any follow up requests are delayed until the first request from the 30 passes the second threshold.
  * The drawback is that this can limit throughput, especially initially.
  * Due to the implemented caching of topics and subscriptions, this should only occur on occassions where there are many different types of events that all get published within a single second. This could theoretically only occur on endpoint startup and should not be an issue later, although there is still a very small chance this can occur.
* Cache entries are removed after the invalidation timeout is due.


