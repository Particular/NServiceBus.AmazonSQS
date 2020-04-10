namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;
    using McMaster.Extensions.CommandLineUtils;

    // usage: 
    // sqs-transport endpoint create name
    // sqs-transport endpoint name add large-message-support bucket-name [--other-options]
    // sqs-transport endpoint name add delay-delivery-support [--other-options]

    // sqs-transport endpoint subscribe name event-type
    // sqs-transport endpoint unsubscribe name event-type    
    // sqs-transport endpoint remove name

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
                    var name = createCommand.Argument("name", "Name of the endpoint (required)").IsRequired();

                    createCommand.Options.Add(accessKey);
                    createCommand.Options.Add(region);
                    createCommand.Options.Add(secret);
                    
                    createCommand.OnExecuteAsync(async ct =>
                    {
                        await CommandRunner.Run(accessKey, secret, region, (sqs, sns, s3) => Endpoint.Create(sqs, name));                       
                    });
                });

                endpointCommand.Command("add", addCommand =>
                {
                    addCommand.Description = "Adds optional infrastructure to an endpoint.";
                    var name = addCommand.Argument("name", "Name of the endpoint (required)").IsRequired();

                    addCommand.Command("large-message-support", largeMessageSupportCommand =>
                    {
                        largeMessageSupportCommand.Description = "Adds large message support infrastructure to an endpoint.";
                         var bucketName = largeMessageSupportCommand.Argument("bucket-name", "Name of the bucket (required)").IsRequired();

                        largeMessageSupportCommand.Options.Add(accessKey);
                        largeMessageSupportCommand.Options.Add(region);
                        largeMessageSupportCommand.Options.Add(secret);                        

                        largeMessageSupportCommand.OnExecuteAsync(async ct =>
                        {
                            await CommandRunner.Run(accessKey, secret, region, (sqs, sns, s3) => Endpoint.AddLargeMessageSupport(s3, name, bucketName));                            
                        });
                    });

                    addCommand.Command("delay-delivery-support", delayDeliverySupportCommand =>
                    {
                        delayDeliverySupportCommand.Description = "Adds delay delivery support infrastructure to an endpoint.";
                        
                        delayDeliverySupportCommand.Options.Add(accessKey);
                        delayDeliverySupportCommand.Options.Add(region);
                        delayDeliverySupportCommand.Options.Add(secret);

                        delayDeliverySupportCommand.OnExecuteAsync(async ct =>
                        {
                            await CommandRunner.Run(accessKey, secret, region, (sqs, sns, s3) => Endpoint.AddDelayDelivery(sqs, name));
                        });
                    });
                });

                endpointCommand.Command("subscribe", subscribeCommand =>
                {
                    subscribeCommand.Description = "Subscribes an endpoint to an event.";
                    var name = subscribeCommand.Argument("name", "Name of the endpoint (required)").IsRequired();
                    var eventType = subscribeCommand.Argument("event-type", "Full name of the event to subscribe to (e.g. MyNamespace.MyMessage) (required)").IsRequired();

                    subscribeCommand.Options.Add(accessKey);
                    subscribeCommand.Options.Add(region);
                    subscribeCommand.Options.Add(secret);

                    /*  
                      var topicName = subscribeCommand.Option("-t|--topic", "Topic name (defaults to 'bundle-1')", CommandOptionType.SingleValue);
                      var subscriptionName = subscribeCommand.Option("-b|--subscription", "Subscription name (defaults to endpoint name) ", CommandOptionType.SingleValue);
                      var shortenedRuleName = subscribeCommand.Option("-r|--rule-name", "Rule name (defaults to event type) ", CommandOptionType.SingleValue);*/

                    subscribeCommand.OnExecuteAsync(async ct =>
                    {
                      //  await CommandRunner.Run(connectionString, client => Endpoint.Subscribe(client, name, topicName, subscriptionName, eventType, shortenedRuleName));

                       await Console.Out.WriteLineAsync($"Endpoint '{name.Value}' subscribed to '{eventType.Value}'.");
                    });
                });

                endpointCommand.Command("unsubscribe", unsubscribeCommand =>
                {
                    unsubscribeCommand.Description = "Unsubscribes an endpoint from an event.";
                    var name = unsubscribeCommand.Argument("name", "Name of the endpoint (required)").IsRequired();
                    var eventType = unsubscribeCommand.Argument("event-type", "Full name of the event to unsubscribe from (e.g. MyNamespace.MyMessage) (required)").IsRequired();

                    unsubscribeCommand.Options.Add(accessKey);
                    unsubscribeCommand.Options.Add(region);
                    unsubscribeCommand.Options.Add(secret);

                    /* 
                     var topicName = unsubscribeCommand.Option("-t|--topic", "Topic name (defaults to 'bundle-1')", CommandOptionType.SingleValue);
                     var subscriptionName = unsubscribeCommand.Option("-b|--subscription", "Subscription name (defaults to endpoint name) ", CommandOptionType.SingleValue);
                     var shortenedRuleName = unsubscribeCommand.Option("-r|--rule-name", "Rule name (defaults to event type) ", CommandOptionType.SingleValue);*/

                    unsubscribeCommand.OnExecuteAsync(async ct =>
                    {
                        //await CommandRunner.Run(connectionString, client => Endpoint.Unsubscribe(client, name, topicName, subscriptionName, eventType, shortenedRuleName));

                        await Console.Out.WriteLineAsync($"Endpoint '{name.Value}' unsubscribed from '{eventType.Value}'.");
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