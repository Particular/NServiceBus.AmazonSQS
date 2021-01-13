namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;
    using System.Collections.Generic;
    using McMaster.Extensions.CommandLineUtils;

    // usage:
    // sqs-transport endpoint create name [--other-options]
    // sqs-transport endpoint add name large-message-support bucket-name [--other-options]
    // sqs-transport endpoint add name delay-delivery-support [--other-options]
    // sqs-transport endpoint subscribe name event-type [--other-options]
    // sqs-transport endpoint unsubscribe name event-type [--other-options]
    // sqs-transport endpoint remove large-message-support bucket-name [--other-options]
    // sqs-transport endpoint remove remove delay-delivery-support [--other-options]
    // sqs-transport endpoint delete name [--other-options]
    // sqs-transport endpoint set-policy name wildcard --account --namespace "namespacename" --prefix "prefix" [--other-options] 
    // sqs-transport endpoint set-policy name events --event-type "event-type1" --event-type "event-type2" [--other-options] 
    // sqs-transport endpoint list-policy name [--other-options]
    class Program
    {
        static int Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "sqs-transport"
            };

            var accessKeyOption = new CommandOption("-i|--access-key-id", CommandOptionType.SingleValue)
            {
                Description = $"Overrides environment variable '{CommandRunner.AccessKeyId}'"
            };

            var secretOption = new CommandOption("-s|--secret", CommandOptionType.SingleValue)
            {
                Description = $"Overrides environment variable '{CommandRunner.SecretAccessKey}'"
            };

            var regionOption = new CommandOption("-r|--region", CommandOptionType.SingleValue)
            {
                Description = $"Overrides environment variable '{CommandRunner.Region}'"
            };

            var prefixOption = new CommandOption("-p|--prefix", CommandOptionType.SingleValue)
            {
                Description = "Prefix to prepend before all queues and topics"
            };

            app.HelpOption(inherited: true);

            app.Command("endpoint", endpointCommand =>
            {
                endpointCommand.OnExecute(() =>
                {
                    Console.WriteLine("Specify a subcommand");
                    endpointCommand.ShowHelp();
                    return 1;
                });

                endpointCommand.Command("create", createCommand =>
                {
                    createCommand.Description = "Creates infrastructure required for an endpoint.";
                    var nameArgument = createCommand.Argument("name", "Name of the endpoint (required)").IsRequired();

                    createCommand.Options.Add(accessKeyOption);
                    createCommand.Options.Add(regionOption);
                    createCommand.Options.Add(secretOption);
                    createCommand.Options.Add(prefixOption);

                    var retentionPeriodInSecondsCommand = createCommand.Option("-t|--retention", "Retention Period in seconds (defaults to " + DefaultConfigurationValues.RetentionPeriod.TotalSeconds + " ) ", CommandOptionType.SingleValue);

                    createCommand.OnExecuteAsync(async ct =>
                    {
                        var endpointName = nameArgument.Value;
                        var retentionPeriodInSeconds = retentionPeriodInSecondsCommand.HasValue() ? double.Parse(retentionPeriodInSecondsCommand.Value()) : DefaultConfigurationValues.RetentionPeriod.TotalSeconds;
                        var prefix = prefixOption.HasValue() ? prefixOption.Value() : DefaultConfigurationValues.QueueNamePrefix;

                        await CommandRunner.Run(accessKeyOption, secretOption, regionOption, (sqs, sns, s3) => Endpoint.Create(sqs, prefix, endpointName, retentionPeriodInSeconds));
                    });
                });

                endpointCommand.Command("delete", deleteCommand =>
                {
                    deleteCommand.Description = "Deletes infrastructure required for an endpoint.";
                    var nameArgument = deleteCommand.Argument("name", "Name of the endpoint (required)").IsRequired();

                    deleteCommand.Options.Add(accessKeyOption);
                    deleteCommand.Options.Add(regionOption);
                    deleteCommand.Options.Add(secretOption);
                    deleteCommand.Options.Add(prefixOption);

                    deleteCommand.OnExecuteAsync(async ct =>
                    {
                        var endpointName = nameArgument.Value;
                        var prefix = prefixOption.HasValue() ? prefixOption.Value() : DefaultConfigurationValues.QueueNamePrefix;
                        await CommandRunner.Run(accessKeyOption, secretOption, regionOption, (sqs, sns, s3) => Endpoint.Delete(sqs, prefix, endpointName));
                    });
                });

                endpointCommand.Command("add", addCommand =>
                {
                    addCommand.Description = "Adds optional infrastructure to an endpoint.";
                    var nameArgument = addCommand.Argument("name", "Name of the endpoint (required)").IsRequired();

                    addCommand.Command("large-message-support", largeMessageSupportCommand =>
                    {
                        largeMessageSupportCommand.Description = "Adds large message support infrastructure to an endpoint.";
                        var bucketArgument = largeMessageSupportCommand.Argument("bucket-name", "Name of the bucket (required).").IsRequired();

                        largeMessageSupportCommand.Options.Add(accessKeyOption);
                        largeMessageSupportCommand.Options.Add(regionOption);
                        largeMessageSupportCommand.Options.Add(secretOption);

                        var keyPrefixCommand = largeMessageSupportCommand.Option("-k|--key-prefix", "S3 Key prefix.", CommandOptionType.SingleValue);
                        var expirationInDaysCommand = largeMessageSupportCommand.Option("-e|--expiration", "Experation time in days (defaults to " + DefaultConfigurationValues.RetentionPeriod.TotalDays + " ) ", CommandOptionType.SingleValue);

                        largeMessageSupportCommand.OnExecuteAsync(async ct =>
                        {
                            var endpointName = nameArgument.Value;
                            var bucketName = bucketArgument.Value;
                            var keyPrefix = keyPrefixCommand.HasValue() ? keyPrefixCommand.Value() : DefaultConfigurationValues.S3KeyPrefix;
                            var expirationInDays = expirationInDaysCommand.HasValue() ? int.Parse(expirationInDaysCommand.Value()) : (int)(Math.Ceiling(DefaultConfigurationValues.RetentionPeriod.TotalDays));

                            await CommandRunner.Run(accessKeyOption, secretOption, regionOption, (sqs, sns, s3) => Endpoint.AddLargeMessageSupport(s3, endpointName, bucketName, keyPrefix, expirationInDays));
                        });
                    });

                    addCommand.Command("delay-delivery-support", delayDeliverySupportCommand =>
                    {
                        delayDeliverySupportCommand.Description = "Adds delay delivery support infrastructure to an endpoint.";

                        delayDeliverySupportCommand.Options.Add(accessKeyOption);
                        delayDeliverySupportCommand.Options.Add(regionOption);
                        delayDeliverySupportCommand.Options.Add(secretOption);
                        delayDeliverySupportCommand.Options.Add(prefixOption);

                        var retentionPeriodInSecondsCommand = delayDeliverySupportCommand.Option("-t|--retention", "Retention period in seconds (defaults to " + DefaultConfigurationValues.RetentionPeriod.TotalSeconds + " ).", CommandOptionType.SingleValue);

                        delayDeliverySupportCommand.OnExecuteAsync(async ct =>
                        {
                            var delayInSeconds = DefaultConfigurationValues.MaximumQueueDelayTime.TotalSeconds;
                            var retentionPeriodInSeconds = retentionPeriodInSecondsCommand.HasValue() ? double.Parse(retentionPeriodInSecondsCommand.Value()) : DefaultConfigurationValues.RetentionPeriod.TotalSeconds;
                            var suffix = DefaultConfigurationValues.DelayedDeliveryQueueSuffix;

                            var endpointName = nameArgument.Value;
                            var prefix = prefixOption.HasValue() ? prefixOption.Value() : DefaultConfigurationValues.QueueNamePrefix;
                            await CommandRunner.Run(accessKeyOption, secretOption, regionOption, (sqs, sns, s3) => Endpoint.AddDelayDelivery(sqs, prefix, endpointName, delayInSeconds, retentionPeriodInSeconds, suffix));
                        });
                    });
                });

                endpointCommand.Command("remove", removeCommand =>
                {
                    removeCommand.Description = "Removes optional infrastructure to an endpoint.";
                    var nameArgument = removeCommand.Argument("name", "Name of the endpoint (required)").IsRequired();

                    removeCommand.Command("large-message-support", largeMessageSupportCommand =>
                    {
                        largeMessageSupportCommand.Description = "Removes large message support infrastructure.";
                        var bucketArgument = largeMessageSupportCommand.Argument("bucket-name", "Name of the bucket (required)").IsRequired();

                        largeMessageSupportCommand.Options.Add(accessKeyOption);
                        largeMessageSupportCommand.Options.Add(regionOption);
                        largeMessageSupportCommand.Options.Add(secretOption);
                        var removeSharedResourcesCommand = largeMessageSupportCommand.Option("-f|--remove-shared-resources", "Remove shared resources (S3 Bucket).", CommandOptionType.NoValue);

                        largeMessageSupportCommand.OnExecuteAsync(async ct =>
                        {
                            var endpointName = nameArgument.Value;
                            var bucketName = bucketArgument.Value;
                            var removeSharedResources = removeSharedResourcesCommand.HasValue();

                            await CommandRunner.Run(accessKeyOption, secretOption, regionOption, (sqs, sns, s3) => Endpoint.RemoveLargeMessageSupport(s3, endpointName, bucketName, removeSharedResources));
                        });
                    });

                    removeCommand.Command("delay-delivery-support", delayDeliverySupportCommand =>
                    {
                        delayDeliverySupportCommand.Description = "Removes delay delivery support infrastructure to an endpoint.";

                        delayDeliverySupportCommand.Options.Add(accessKeyOption);
                        delayDeliverySupportCommand.Options.Add(regionOption);
                        delayDeliverySupportCommand.Options.Add(secretOption);
                        delayDeliverySupportCommand.Options.Add(prefixOption);

                        delayDeliverySupportCommand.OnExecuteAsync(async ct =>
                        {
                            var endpointName = nameArgument.Value;
                            var prefix = prefixOption.HasValue() ? prefixOption.Value() : DefaultConfigurationValues.QueueNamePrefix;
                            var suffix = DefaultConfigurationValues.DelayedDeliveryQueueSuffix;

                            await CommandRunner.Run(accessKeyOption, secretOption, regionOption, (sqs, sns, s3) => Endpoint.RemoveDelayDelivery(sqs, prefix, endpointName, suffix));
                        });
                    });
                });

                endpointCommand.Command("subscribe", subscribeCommand =>
                {
                    subscribeCommand.Description = "Subscribes an endpoint to an event.";
                    var nameArgument = subscribeCommand.Argument("name", "Name of the endpoint (required)").IsRequired();
                    var eventTypeArgument = subscribeCommand.Argument("event-type", "Full name of the event to subscribe to (e.g. MyNamespace.MyMessage) (required)").IsRequired();

                    subscribeCommand.Options.Add(accessKeyOption);
                    subscribeCommand.Options.Add(regionOption);
                    subscribeCommand.Options.Add(secretOption);
                    subscribeCommand.Options.Add(prefixOption);

                    subscribeCommand.OnExecuteAsync(async ct =>
                    {
                        var endpointName = nameArgument.Value;
                        var prefix = prefixOption.HasValue() ? prefixOption.Value() : DefaultConfigurationValues.QueueNamePrefix;
                        var eventType = eventTypeArgument.Value;

                        await CommandRunner.Run(accessKeyOption, secretOption, regionOption, (sqs, sns, s3) => Endpoint.Subscribe(sqs, sns, prefix, endpointName, eventType));
                    });
                });

                endpointCommand.Command("unsubscribe", unsubscribeCommand =>
                {
                    unsubscribeCommand.Description = "Unsubscribes an endpoint from an event.";
                    var nameArgument = unsubscribeCommand.Argument("name", "Name of the endpoint (required)").IsRequired();
                    var eventTypeArgument = unsubscribeCommand.Argument("event-type", "Full name of the event to unsubscribe from (e.g. MyNamespace.MyMessage) (required)").IsRequired();

                    unsubscribeCommand.Options.Add(accessKeyOption);
                    unsubscribeCommand.Options.Add(regionOption);
                    unsubscribeCommand.Options.Add(secretOption);
                    unsubscribeCommand.Options.Add(prefixOption);
                    var removeSharedResourcesCommand = unsubscribeCommand.Option("-f|--remove-shared-resources", "Remove shared resources (Topic being unsubscribed from).", CommandOptionType.NoValue);

                    unsubscribeCommand.OnExecuteAsync(async ct =>
                    {
                        var endpointName = nameArgument.Value;
                        var prefix = prefixOption.HasValue() ? prefixOption.Value() : DefaultConfigurationValues.QueueNamePrefix;
                        var eventType = eventTypeArgument.Value;
                        var removeSharedResources = removeSharedResourcesCommand.HasValue();

                        await CommandRunner.Run(accessKeyOption, secretOption, regionOption, (sqs, sns, s3) => Endpoint.Unsubscribe(sqs, sns, prefix, endpointName, eventType, removeSharedResources));

                        await Console.Out.WriteLineAsync($"Endpoint '{endpointName}' unsubscribed from '{eventType}'.");
                    });
                });
            
                endpointCommand.Command("set-policy", policyCommand => 
                {
                    policyCommand.Description = "Sets the IAM policy for an endpoint.";
                    var nameArgument = policyCommand.Argument("name", "Name of the endpoint (required)").IsRequired();
                       
                    policyCommand.OnExecute(() =>
                    {
                        Console.WriteLine("Specify a subcommand");
                        policyCommand.ShowHelp();
                        return 1;
                    });

                    policyCommand.Command("events", policyBasedOneventsCommand =>
                    {
                        policyBasedOneventsCommand.Options.Add(accessKeyOption);
                        policyBasedOneventsCommand.Options.Add(regionOption);
                        policyBasedOneventsCommand.Options.Add(secretOption);
                        policyBasedOneventsCommand.Options.Add(prefixOption);

                        var eventTypeOption = new CommandOption("-evt|--event-type", CommandOptionType.MultipleValue)
                        {
                            Description = "Allow subscription to topic for specific event type."
                        };
                        policyBasedOneventsCommand.Options.Add(eventTypeOption);

                        policyBasedOneventsCommand.OnExecuteAsync(async ct =>
                        {
                            var endpointName = nameArgument.Value;
                            var prefix = prefixOption.HasValue() ? prefixOption.Value() : DefaultConfigurationValues.QueueNamePrefix;
                            var eventTypes = eventTypeOption.HasValue() ? eventTypeOption.Values : new List<string>();

                            await CommandRunner.Run(accessKeyOption, secretOption, regionOption, (sqs, sns, s3) => Endpoint.SetPolicy(sqs, sns, prefix, endpointName, eventTypes, false, false, new List<string>()));
                        });
                    });

                    policyCommand.Command("wildcard", policyBasedOnWildcardsCommand =>
                    {
                        policyBasedOnWildcardsCommand.Options.Add(accessKeyOption);
                        policyBasedOnWildcardsCommand.Options.Add(regionOption);                        
                        policyBasedOnWildcardsCommand.Options.Add(secretOption);
                        policyBasedOnWildcardsCommand.Options.Add(prefixOption);

                        var accountOption = new CommandOption("-a|--account", CommandOptionType.NoValue)
                        {
                            Description = "Allow subscription to all topics in an account."
                        };
                        policyBasedOnWildcardsCommand.Options.Add(accountOption);

                        var namespaceOption = new CommandOption("-n|--namespace", CommandOptionType.MultipleValue)
                        {
                            Description = "Allow subscription to topics for events in a specific namespace."
                        };
                        policyBasedOnWildcardsCommand.Options.Add(namespaceOption);

                        policyBasedOnWildcardsCommand.OnExecuteAsync(async ct =>
                        {
                            var endpointName = nameArgument.Value;
                            var addAccountCondition = accountOption.HasValue();
                            var addPrefixcondition = prefixOption.HasValue();
                            var prefix = addPrefixcondition ? prefixOption.Value() : DefaultConfigurationValues.QueueNamePrefix;                            
                            var namespaceConditions = namespaceOption.HasValue() ? namespaceOption.Values : new List<string>();
                            var eventTypes = new List<string>();

                            await CommandRunner.Run(accessKeyOption, secretOption, regionOption, (sqs, sns, s3) => Endpoint.SetPolicy(sqs, sns, prefix, endpointName, eventTypes, addAccountCondition, addPrefixcondition, namespaceConditions));
                        });
                    });
                });
                
                endpointCommand.Command("list-policy", listPolicyCommand =>
                {
                    listPolicyCommand.Description = "Sets the IAM policy for an endpoint.";
                    var nameArgument = listPolicyCommand.Argument("name", "Name of the endpoint (required)").IsRequired();

                    listPolicyCommand.Options.Add(accessKeyOption);
                    listPolicyCommand.Options.Add(regionOption);                        
                    listPolicyCommand.Options.Add(secretOption);
                    listPolicyCommand.Options.Add(prefixOption);

                    listPolicyCommand.OnExecuteAsync(async ct =>
                    {
                        var endpointName = nameArgument.Value;
                        var prefix = prefixOption.HasValue() ? prefixOption.Value() : DefaultConfigurationValues.QueueNamePrefix;                            

                        await CommandRunner.Run(accessKeyOption, secretOption, regionOption, (sqs, sns, s3) => Endpoint.ListPolicy(sqs, sns, prefix, endpointName));
                    });
                });
            
            });

            app.OnExecute(() =>
            {
                Console.WriteLine("Specify a subcommand");
                app.ShowHelp();
                return 1;
            });

            try
            {
                return app.Execute(args);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Command failed with exception ({exception.GetType().Name}): {exception.Message}");
                return 1;
            }
        }
    }
}