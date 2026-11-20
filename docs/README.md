# Engineering context

## About this repository

The NServiceBus.AmazonSQS repository implements the Amazon SQS transport for NServiceBus.

- `src/` — transport implementation and tests
- `docs/` — design documentation
- `.github/workflows/` — CI pipelines

## Start here

- [Amazon SQS transport documentation](https://docs.particular.net/transports/sqs/) — configuration and usage
- [README.md](../README.md) — how to build, run, and test locally
- [Contributing](https://docs.particular.net/platform/contributing) — contribution process

## Architecture and design

- [Publish-subscribe compatibility mode](message-drive-pub-sub-compat.md) — how compatibility mode avoids duplicate delivery while migrating from message-driven to SNS-based publish-subscribe

- [Delayed delivery](https://docs.particular.net/transports/sqs/delayed-delivery) — why longer delays need transport support on both the sending and receiving endpoints

## Upstream contracts

- [NServiceBus: message dispatchers](https://github.com/Particular/NServiceBus/blob/master/docs/message-dispatchers.md) — what the transport must honor when dispatching, including delivery properties and dispatch consistency
- [NServiceBus: message receivers](https://github.com/Particular/NServiceBus/blob/master/docs/message-receivers.md) — what the transport must honor when receiving, including concurrency and shutdown
- [NServiceBus: for downstream components](https://github.com/Particular/NServiceBus/blob/master/docs/README.md#for-downstream-components) — the index of Core contracts for downstream components

## Decisions and rationale

- [Architecture and design decisions](decisions/)

Keep this index current when a canonical source is added, replaced, or retired; link, do not copy.
