namespace NServiceBus.AcceptanceTests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Configuration.AdvancedExtensibility;
using Extensibility;
using Features;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using Persistence;
using Unicast.Subscriptions;
using Unicast.Subscriptions.MessageDrivenSubscriptions;

public class TestingInMemoryPersistence : PersistenceDefinition, IPersistenceDefinitionFactory<TestingInMemoryPersistence>
{
    TestingInMemoryPersistence() => Supports<StorageType.Subscriptions, TestingInMemorySubscriptionPersistence>();

    public static TestingInMemoryPersistence Create() => new();
}

public static class InMemoryPersistenceExtensions
{
    public static void UseStorage(this PersistenceExtensions<TestingInMemoryPersistence> extensions, TestingInMemorySubscriptionStorage storageInstance) => extensions.GetSettings().Set("InMemoryPersistence.StorageInstance", storageInstance);
}

public class TestingInMemorySubscriptionPersistence : Features.Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        var storageInstance = context.Settings.GetOrDefault<TestingInMemorySubscriptionStorage>("InMemoryPersistence.StorageInstance");
        context.Services.AddSingleton<ISubscriptionStorage>(storageInstance ?? new TestingInMemorySubscriptionStorage());
    }
}

public class TestingInMemorySubscriptionStorage : ISubscriptionStorage
{
    public Task Subscribe(Subscriber subscriber, MessageType messageType, ContextBag context, CancellationToken cancellationToken)
    {
        var dict = storage.GetOrAdd(messageType, type => new ConcurrentDictionary<string, Subscriber>(StringComparer.OrdinalIgnoreCase));

        dict.AddOrUpdate(BuildKey(subscriber), _ => subscriber, (_, __) => subscriber);
        return Task.FromResult(true);
    }

    static string BuildKey(Subscriber subscriber) => $"{subscriber.TransportAddress ?? ""}_{subscriber.Endpoint ?? ""}";

    public Task Unsubscribe(Subscriber subscriber, MessageType messageType, ContextBag context, CancellationToken cancellationToken)
    {
        if (storage.TryGetValue(messageType, out var dict))
        {
            dict.TryRemove(BuildKey(subscriber), out var _);
        }

        return Task.FromResult(true);
    }

    public Task<IEnumerable<Subscriber>> GetSubscriberAddressesForMessage(IEnumerable<MessageType> messageTypes, ContextBag context, CancellationToken cancellationToken)
    {
        var subscribers = messageTypes
            .SelectMany(msgType => storage.TryGetValue(msgType, out var subs) ? subs.Values : [])
            .GroupBy(s => new
            {
                s.TransportAddress,
                s.Endpoint
            }) // Subscriber does not implement IEquatable<T>
            .Select(g => g.First());

        return Task.FromResult(subscribers);
    }

    readonly ConcurrentDictionary<MessageType, ConcurrentDictionary<string, Subscriber>> storage = new();
}