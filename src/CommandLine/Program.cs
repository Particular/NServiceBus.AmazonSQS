namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;
    using McMaster.Extensions.CommandLineUtils;

    // usage: 
    // sqs-transport endpoint create name [--other-options]
    // sqs-transport endpoint name add large-message-support bucket-name [--other-options]
    // sqs-transport endpoint name add delay-delivery-support [--other-options]
    // sqs-transport endpoint subscribe name event-type [--other-options]

    // sqs-transport endpoint unsubscribe name event-type [--other-options]
    // sqs-transport endpoint name remove large-message-support bucket-name [--other-options]
    // sqs-transport endpoint name remove delay-delivery-support [--other-options]
    // sqs-transport endpoint delete name [--other-options]

    class Program
    {
        static int Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "sqs-transport"
            };

            var accessKey = new CommandOption("-k|--access-key-id", CommandOptionType.SingleValue)
            {
                Description = $"Overrides environment variable '{CommandRunner.AccessKeyId}'"
            };

            var secret = new CommandOption("-s|--secret", CommandOptionType.SingleValue)
            {
                Description = $"Overrides environment variable '{CommandRunner.SecretAccessKey}'"
            };

            var region = new CommandOption("-r|--region", CommandOptionType.SingleValue)
            {
                Description = $"Overrides environment variable '{CommandRunner.Region}'"
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

                    createCommand.Options.Add(accessKey);
                    createCommand.Options.Add(region);
                    createCommand.Options.Add(secret);

                    var retentionPeriodInSecondsCommand = createCommand.Option("-t|--retention", "Retention Period in seconds (defaults to " + DefaultConfigurationValues.RetentionPeriod.TotalSeconds + " ) ", CommandOptionType.SingleValue);
                    
                    createCommand.OnExecuteAsync(async ct =>
                    {
                        var endpointName = nameArgument.Value;
                        var retentionPeriodInSeconds = retentionPeriodInSecondsCommand.HasValue() ? double.Parse(retentionPeriodInSecondsCommand.Value()) : DefaultConfigurationValues.RetentionPeriod.TotalSeconds;

                        await CommandRunner.Run(accessKey, secret, region, (sqs, sns, s3) => Endpoint.Create(sqs, endpointName, retentionPeriodInSeconds));                       
                    });
                });

                endpointCommand.Command("delete", createCommand =>
                {
                    createCommand.Description = "Deletes infrastructure required for an endpoint.";
                    var nameArgument = createCommand.Argument("name", "Name of the endpoint (required)").IsRequired();

                    createCommand.Options.Add(accessKey);
                    createCommand.Options.Add(region);
                    createCommand.Options.Add(secret);

                    createCommand.OnExecuteAsync(async ct =>
                    {
                        var endpointName = nameArgument.Value;
                        await CommandRunner.Run(accessKey, secret, region, (sqs, sns, s3) => Endpoint.Delete(sqs, endpointName));
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

                        largeMessageSupportCommand.Options.Add(accessKey);
                        largeMessageSupportCommand.Options.Add(region);
                        largeMessageSupportCommand.Options.Add(secret);

                        var prefixCommand = largeMessageSupportCommand.Option("-p|--prefix", "S3 Key prefix.", CommandOptionType.SingleValue);
                        var expirationInDaysCommand = largeMessageSupportCommand.Option("-e|--expiration", "Experation time in days (defaults to " + DefaultConfigurationValues.RetentionPeriod.TotalDays + " ) ", CommandOptionType.SingleValue);

                        largeMessageSupportCommand.OnExecuteAsync(async ct =>
                        {
                            var endpointName = nameArgument.Value;
                            var bucketName = bucketArgument.Value;
                            var prefix = prefixCommand.HasValue() ? prefixCommand.Value() : DefaultConfigurationValues.S3KeyPrefix;
                            var expirationInDays = expirationInDaysCommand.HasValue() ? int.Parse(expirationInDaysCommand.Value()) : (int)(Math.Ceiling(DefaultConfigurationValues.RetentionPeriod.TotalDays));

                            await CommandRunner.Run(accessKey, secret, region, (sqs, sns, s3) => Endpoint.AddLargeMessageSupport(s3, endpointName, bucketName, prefix, expirationInDays));                            
                        });
                    });

                    addCommand.Command("delay-delivery-support", delayDeliverySupportCommand =>
                    {
                        delayDeliverySupportCommand.Description = "Adds delay delivery support infrastructure to an endpoint.";
                        
                        delayDeliverySupportCommand.Options.Add(accessKey);
                        delayDeliverySupportCommand.Options.Add(region);
                        delayDeliverySupportCommand.Options.Add(secret);

                        var delayInSecondsCommand = delayDeliverySupportCommand.Option("-d|--delay", "Delay in seconds (defaults to " + DefaultConfigurationValues.MaximumQueueDelayTime.TotalSeconds + " ).", CommandOptionType.SingleValue);
                        var retentionPeriodInSecondsCommand = delayDeliverySupportCommand.Option("-t|--retention", "Retention period in seconds (defaults to " + DefaultConfigurationValues.RetentionPeriod.TotalSeconds + " ).", CommandOptionType.SingleValue);
                        var suffixCommand = delayDeliverySupportCommand.Option("-s|--suffix", "Delayed delivery queue suffix (defaults to " + DefaultConfigurationValues.DelayedDeliveryQueueSuffix + " ) .", CommandOptionType.SingleValue);

                        delayDeliverySupportCommand.OnExecuteAsync(async ct =>
                        {
                            var delayInSeconds = delayInSecondsCommand.HasValue() ? double.Parse(delayInSecondsCommand.Value()) : DefaultConfigurationValues.MaximumQueueDelayTime.TotalSeconds;
                            var retentionPeriodInSeconds = retentionPeriodInSecondsCommand.HasValue() ? double.Parse(retentionPeriodInSecondsCommand.Value()) : DefaultConfigurationValues.RetentionPeriod.TotalSeconds;
                            var suffix = suffixCommand.HasValue() ? suffixCommand.Value() : DefaultConfigurationValues.DelayedDeliveryQueueSuffix;

                            var endpointName = nameArgument.Value;
                            await CommandRunner.Run(accessKey, secret, region, (sqs, sns, s3) => Endpoint.AddDelayDelivery(sqs, endpointName, delayInSeconds, retentionPeriodInSeconds, suffix));
                        });
                    });
                });

                endpointCommand.Command("remove", addCommand =>
                {
                    addCommand.Description = "Removes optional infrastructure to an endpoint.";
                    var nameArgument = addCommand.Argument("name", "Name of the endpoint (required)").IsRequired();

                    addCommand.Command("large-message-support", largeMessageSupportCommand =>
                    {
                        largeMessageSupportCommand.Description = "Removes large message support infrastructure.";
                        var bucketArgument = largeMessageSupportCommand.Argument("bucket-name", "Name of the bucket (required)").IsRequired();

                        largeMessageSupportCommand.Options.Add(accessKey);
                        largeMessageSupportCommand.Options.Add(region);
                        largeMessageSupportCommand.Options.Add(secret);

                        largeMessageSupportCommand.OnExecuteAsync(async ct =>
                        {
                            var endpointName = nameArgument.Value;
                            var bucketName = bucketArgument.Value;

                            await CommandRunner.Run(accessKey, secret, region, (sqs, sns, s3) => Endpoint.RemoveLargeMessageSupport(s3, endpointName, bucketName));
                        });
                    });

                    addCommand.Command("delay-delivery-support", delayDeliverySupportCommand =>
                    {
                        delayDeliverySupportCommand.Description = "Removes delay delivery support infrastructure to an endpoint.";

                        delayDeliverySupportCommand.Options.Add(accessKey);
                        delayDeliverySupportCommand.Options.Add(region);
                        delayDeliverySupportCommand.Options.Add(secret);

                        delayDeliverySupportCommand.OnExecuteAsync(async ct =>
                        {
                            var endpointName = nameArgument.Value;
                            string suffix = DefaultConfigurationValues.DelayedDeliveryQueueSuffix;

                            await CommandRunner.Run(accessKey, secret, region, (sqs, sns, s3) => Endpoint.RemoveDelayDelivery(sqs, endpointName, suffix));
                        });
                    });
                });

                endpointCommand.Command("subscribe", subscribeCommand =>
                {
                    subscribeCommand.Description = "Subscribes an endpoint to an event.";
                    var nameArgument = subscribeCommand.Argument("name", "Name of the endpoint (required)").IsRequired();
                    var eventTypeArgument = subscribeCommand.Argument("event-type", "Full name of the event to subscribe to (e.g. MyNamespace.MyMessage) (required)").IsRequired();

                    subscribeCommand.Options.Add(accessKey);
                    subscribeCommand.Options.Add(region);
                    subscribeCommand.Options.Add(secret);
                    subscribeCommand.OnExecuteAsync(async ct =>
                    {
                        var endpointName = nameArgument.Value;
                        var eventType = eventTypeArgument.Value;

                        await CommandRunner.Run(accessKey, secret, region, (sqs, sns, s3) => Endpoint.Subscribe(sqs, sns, endpointName, eventType));                        
                    });
                });

                endpointCommand.Command("unsubscribe", unsubscribeCommand =>
                {
                    unsubscribeCommand.Description = "Unsubscribes an endpoint from an event.";
                    var nameArgument = unsubscribeCommand.Argument("name", "Name of the endpoint (required)").IsRequired();
                    var eventTypeArgument = unsubscribeCommand.Argument("event-type", "Full name of the event to unsubscribe from (e.g. MyNamespace.MyMessage) (required)").IsRequired();

                    unsubscribeCommand.Options.Add(accessKey);
                    unsubscribeCommand.Options.Add(region);
                    unsubscribeCommand.Options.Add(secret);

                    unsubscribeCommand.OnExecuteAsync(async ct =>
                    {
                        var endpointName = nameArgument.Value;
                        var eventType = eventTypeArgument.Value;

                        await CommandRunner.Run(accessKey, secret, region, (sqs, sns, s3) => Endpoint.Unsubscribe(sqs, sns, endpointName, eventType));

                        await Console.Out.WriteLineAsync($"Endpoint '{endpointName}' unsubscribed from '{eventType}'.");
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