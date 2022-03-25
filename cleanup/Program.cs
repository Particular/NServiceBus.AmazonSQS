using System.Diagnostics;
using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;

public class Program
{
    public static async Task Main()
    {
        var accessKeyId = Environment.GetEnvironmentVariable("CLEANUP_AWS_ACCESS_KEY_ID");
        var secretAccessKey = Environment.GetEnvironmentVariable("CLEANUP_AWS_SECRET_ACCESS_KEY");
        var region = Environment.GetEnvironmentVariable("AWS_REGION");

        var config = new AmazonSQSConfig { ThrottleRetries = false, RegionEndpoint = RegionEndpoint.GetBySystemName(region) };
        var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);
        sqsClient = new AmazonSQSClient(credentials, config);

        // Known prefixes that can and should be cleaned up are:
        // AT    => acceptance tests
        // TT    => transport tests
        // cli-  => Tests of the command line tool
        // CiWin => Older format for CI tests on Windows
        // CiNix => Older format for CI tests on Linux
        //
        // But the only ones that need to stick around start with FixedAT because there are tests
        // that due to eventual consistency of applying policies can only run correctly if the
        // infrastructure is pre-created.

        Console.WriteLine($"Beginning cleanup of queues older than 8 hours old.");
        Console.WriteLine($"Current Time is {DateTime.UtcNow:u}, Cutoff is {cutoff:u}");

        var request = new ListQueuesRequest(string.Empty) { MaxResults = 1000 };

        while (true)
        {
            Console.WriteLine("Getting batch of 1000 queues.");
            var result = await sqsClient.ListQueuesAsync(request);
            request.NextToken = result.NextToken;

            var tasks = result.QueueUrls.Select(url => RemoveQueueIfNecessary(url)).ToArray();

            await Task.WhenAll(tasks);

            if (request.NextToken == null)
            {
                break;
            }
        }
    }

    static async Task RemoveQueueIfNecessary(string url)
    {
        string queueName = url.Split('/')[4];

        if (queueName.StartsWith("FixedAT"))
        {
            return;
        }

        try
        {
            while (!processing)
            {
                await Task.Delay(100);
            }

            var details = await sqsClient.GetQueueAttributesAsync(url, attributes);
            var createdUtc = details.CreatedTimestamp.ToUniversalTime();

            if (createdUtc > cutoff)
            {
                return;
            }

            Console.WriteLine($"Deleting {queueName} created {createdUtc:u}");
            await sqsClient.DeleteQueueAsync(url);
        }
        catch (AmazonSQSException x) when (x.ErrorCode == "RequestThrottled")
        {
            if (processing)
            {
                processing = false;
                Console.WriteLine("Throttled, pausing for 30s");
                await Task.Delay(30000);
                processing = true;
            }
        }
        catch (AmazonSQSException sx) when (sx.ErrorCode == "AWS.SimpleQueueService.NonExistentQueue")
        {
            // It's already gone
        }
        catch (IOException iox)
        {
            // Network issue, we'll get it next time
            Console.WriteLine("Ignoring: " + iox.Message);
        }
    }

    static AmazonSQSClient sqsClient;
    static readonly List<string> attributes = new() { "CreatedTimestamp" };
    static readonly DateTime cutoff = DateTime.UtcNow.AddHours(-8);
    static bool processing = true;
}