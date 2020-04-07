namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;
    using McMaster.Extensions.CommandLineUtils;

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
                    createCommand.Description = "Creates required infrastructure for an endpoint.";
                    var name = createCommand.Argument("name", "Name of the endpoint (required)").IsRequired();

                    createCommand.Options.Add(accessKey);
                    createCommand.Options.Add(region);
                    createCommand.Options.Add(secret);

                    /*    createCommand.Options.Add(size);
                        createCommand.Options.Add(partitioning);
                        var topicName = createCommand.Option("-t|--topic", "Topic name (defaults to 'bundle-1')", CommandOptionType.SingleValue);
                        var subscriptionName = createCommand.Option("-b|--subscription", "Subscription name (defaults to endpoint name) ", CommandOptionType.SingleValue);*/

                    createCommand.OnExecuteAsync(async ct =>
                    {
                       // await CommandRunner.Run(connectionString, client => Endpoint.Create(client, name, topicName, subscriptionName, size, partitioning));

                       await Console.Out.WriteLineAsync($"Endpoint '{name.Value}' is ready.");
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

            app.Command("queue", queueCommand =>
            {
                queueCommand.OnExecute(() =>
                {
                    Console.WriteLine("Specify a subcommand");
                    queueCommand.ShowHelp();
                    return 1;
                });

                queueCommand.Command("create", createCommand =>
                {
                    createCommand.Description = "Creates a queue with the settings required by the transport";
                    var name = createCommand.Argument("name", "Name of the queue (required)").IsRequired();

                    createCommand.Options.Add(accessKey);
                    createCommand.Options.Add(region);
                    createCommand.Options.Add(secret);

                    /* 
                     createCommand.Options.Add(size);
                     createCommand.Options.Add(partitioning);*/

                    createCommand.OnExecuteAsync(async ct =>
                    {
                      //  await CommandRunner.Run(connectionString, client => Queue.Create(client, name, size, partitioning));

                        await Console.Out.WriteLineAsync($"Queue name '{name.Value}' created");
                    });
                });

                queueCommand.Command("delete", deleteCommand =>
                {
                    deleteCommand.Description = "Deletes a queue";
                    var name = deleteCommand.Argument("name", "Name of the queue (required)").IsRequired();

                    deleteCommand.Options.Add(accessKey);
                    deleteCommand.Options.Add(region);
                    deleteCommand.Options.Add(secret);

                    deleteCommand.OnExecuteAsync(async ct =>
                    {
                        // await CommandRunner.Run(connectionString, client => Queue.Delete(client, name));

                        await Console.Out.WriteLineAsync($"Queue with name '{name.Value}' deleted");
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