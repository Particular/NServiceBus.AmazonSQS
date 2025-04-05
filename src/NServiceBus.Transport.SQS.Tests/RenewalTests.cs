namespace NServiceBus.Transport.SQS.Tests;

using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

[TestFixture]
public class RenewalTests
{
    [Theory]
    [
        TestCase(0, 0),
        TestCase(1, 0.5),
        TestCase(20, 10),
        TestCase(30, 20),
        TestCase(160, 150)
    ]
    public void Should_calculate_correctly(int renewalTime, double expected)
    {
        var fixedTime = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var fakeTimeProvider = new FakeTimeProvider(fixedTime);
        var visibilityTimeExpiresOn = fixedTime.AddSeconds(renewalTime);

        var result = Renewal.CalculateRenewalTime(visibilityTimeExpiresOn, fakeTimeProvider);

        Assert.That(result, Is.EqualTo(TimeSpan.FromSeconds(expected)));
    }

    [Test]
    public void Should_return_zero_delay_for_remaining_time_smaller_than_minimum_threshold()
    {
        var fixedTime = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var fakeTimeProvider = new FakeTimeProvider(fixedTime);
        var visibilityTimeExpiresOn = fixedTime.AddMilliseconds(300);

        var result = Renewal.CalculateRenewalTime(visibilityTimeExpiresOn, fakeTimeProvider);

        Assert.That(result, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public async Task Should_renew_until_cancelled_according_to_renewal_time()
    {
        var message = new Message();

        var tokenSource = new CancellationTokenSource();
        var messageVisibilityLostCancellationTokenSource = new CancellationTokenSource();
        var sqsClient = new MockSqsClient();
        var fakeTimeProvider = new FakeTimeProvider();

        var expiresOn = fakeTimeProvider.Start.AddSeconds(10);

        var renewalTask = Renewal.RenewMessageVisibility(message, expiresOn, visibilityTimeoutInSeconds: 10, sqsClient: sqsClient, inputQueueUrl: "inputQueue", messageVisibilityLostCancellationTokenSource, timeProvider: fakeTimeProvider, cancellationToken: tokenSource.Token);

        // advance time twice to simulate more than one renewal
        fakeTimeProvider.Advance(TimeSpan.FromSeconds(5));
        fakeTimeProvider.Advance(TimeSpan.FromSeconds(11));

        await tokenSource.CancelAsync();

        var result = await renewalTask;

        Assert.That(sqsClient.ChangeMessageVisibilityRequestsSent, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            // since the message still has 5 seconds left we are trying to renew it for the ten seconds visibility time plus the expiry window
            Assert.That(sqsClient.ChangeMessageVisibilityRequestsSent.ElementAt(0).VisibilityTimeout, Is.EqualTo(15));
            // since the message still has 4 seconds left we are trying to renew it for the ten seconds visibility time plus the expiry window
            Assert.That(sqsClient.ChangeMessageVisibilityRequestsSent.ElementAt(1).VisibilityTimeout, Is.EqualTo(14));
            Assert.That(result, Is.EqualTo(Renewal.Result.Stopped));
        });
    }

    [Test]
    public async Task Should_not_renew_when_time_passed_below_renewal_time()
    {
        var message = new Message();

        var tokenSource = new CancellationTokenSource();
        var messageVisibilityLostCancellationTokenSource = new CancellationTokenSource();
        var sqsClient = new MockSqsClient();
        var fakeTimeProvider = new FakeTimeProvider();

        var expiresOn = fakeTimeProvider.Start.AddSeconds(30);

        var renewalTask = Renewal.RenewMessageVisibility(message, expiresOn, visibilityTimeoutInSeconds: 10, sqsClient: sqsClient, inputQueueUrl: "inputQueue", messageVisibilityLostCancellationTokenSource, timeProvider: fakeTimeProvider, cancellationToken: tokenSource.Token);

        // advance time but still below renewal time
        fakeTimeProvider.Advance(TimeSpan.FromSeconds(18));

        await tokenSource.CancelAsync();

        var result = await renewalTask;

        Assert.Multiple(() =>
        {
            Assert.That(sqsClient.ChangeMessageVisibilityRequestsSent, Is.Empty);
            Assert.That(result, Is.EqualTo(Renewal.Result.Stopped));
        });
    }

    [Test]
    public async Task Should_renew_when_expired_but_below_threshold()
    {
        var message = new Message();

        var tokenSource = new CancellationTokenSource();
        var messageVisibilityLostCancellationTokenSource = new CancellationTokenSource();
        var sqsClient = new MockSqsClient();
        var fakeTimeProvider = new FakeTimeProvider();

        // Message is already expired
        var expiresOn = fakeTimeProvider.Start.AddSeconds(-2);

        var renewalTask = Renewal.RenewMessageVisibility(message, expiresOn, visibilityTimeoutInSeconds: 10, sqsClient: sqsClient, inputQueueUrl: "inputQueue", messageVisibilityLostCancellationTokenSource, timeProvider: fakeTimeProvider, cancellationToken: tokenSource.Token);

        // advance time slightly
        fakeTimeProvider.Advance(TimeSpan.FromMilliseconds(300));

        await tokenSource.CancelAsync();

        var result = await renewalTask;

        Assert.That(sqsClient.ChangeMessageVisibilityRequestsSent, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            // since the message expired two seconds ago we are trying to renew it for the ten seconds visibility time plus the expiry window
            Assert.That(sqsClient.ChangeMessageVisibilityRequestsSent.ElementAt(0).VisibilityTimeout, Is.EqualTo(12));
            Assert.That(result, Is.EqualTo(Renewal.Result.Stopped));
        });
    }

    [Test]
    public async Task Should_renew_when_expired_but_below_buffer()
    {
        var message = new Message();

        var tokenSource = new CancellationTokenSource();
        var messageVisibilityLostCancellationTokenSource = new CancellationTokenSource();
        var sqsClient = new MockSqsClient();
        var fakeTimeProvider = new FakeTimeProvider();

        var expiresOn = fakeTimeProvider.Start.AddSeconds(160);

        var renewalTask = Renewal.RenewMessageVisibility(message, expiresOn, visibilityTimeoutInSeconds: 10, sqsClient: sqsClient, inputQueueUrl: "inputQueue", messageVisibilityLostCancellationTokenSource, timeProvider: fakeTimeProvider, cancellationToken: tokenSource.Token);

        // advanced to renewal time
        fakeTimeProvider.Advance(TimeSpan.FromSeconds(150));

        await tokenSource.CancelAsync();

        var result = await renewalTask;

        Assert.That(sqsClient.ChangeMessageVisibilityRequestsSent, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            // since the message is about to expire within the 10 seconds buffer we are trying to renew it for the ten seconds visibility time plus the expiry window
            Assert.That(sqsClient.ChangeMessageVisibilityRequestsSent.ElementAt(0).VisibilityTimeout, Is.EqualTo(20));
            Assert.That(result, Is.EqualTo(Renewal.Result.Stopped));
        });
    }

    [Test]
    public async Task Should_notify_about_visibility_extension_failing()
    {
        var message = new Message();

        var tokenSource = new CancellationTokenSource();
        using var messageVisibilityLostCancellationTokenSource = new CancellationTokenSource();
        var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var _ = messageVisibilityLostCancellationTokenSource.Token.Register(() => completionSource.TrySetResult());
        var sqsClient = new MockSqsClient();
        var fakeTimeProvider = new FakeTimeProvider();

        sqsClient.ChangeMessageVisibilityRequestResponse = (req, _) => throw new AmazonSQSException("Simulated exception", ErrorType.Sender, "InvalidParameterValue", "RequestId", HttpStatusCode.BadRequest);

        var expiresOn = fakeTimeProvider.Start.AddSeconds(10);

        var renewalTask = Renewal.RenewMessageVisibility(message, expiresOn, visibilityTimeoutInSeconds: 10, sqsClient: sqsClient, inputQueueUrl: "inputQueue", messageVisibilityLostCancellationTokenSource, timeProvider: fakeTimeProvider, cancellationToken: tokenSource.Token);

        // advanced to renewal time
        fakeTimeProvider.Advance(TimeSpan.FromSeconds(5));

        await completionSource.Task;
        var result = await renewalTask;

        Assert.Multiple(() =>
        {
            Assert.That(sqsClient.ChangeMessageVisibilityRequestsSent, Has.Count.EqualTo(1));
            Assert.That(result, Is.EqualTo(Renewal.Result.Failed));
        });
    }

    [Test]
    public async Task Should_indicate_stopped_when_cancelled_from_beginning()
    {
        var message = new Message();

        var messageVisibilityLostCancellationTokenSource = new CancellationTokenSource();
        var sqsClient = new MockSqsClient();
        var fakeTimeProvider = new FakeTimeProvider();

        var cancelledToken = new CancellationToken(true);

        var renewalTask = Renewal.RenewMessageVisibility(message, fakeTimeProvider.Start, visibilityTimeoutInSeconds: 10, sqsClient: sqsClient, inputQueueUrl: "inputQueue", messageVisibilityLostCancellationTokenSource, timeProvider: fakeTimeProvider, cancellationToken: cancelledToken);

        var result = await renewalTask;

        Assert.Multiple(() =>
        {
            Assert.That(sqsClient.ChangeMessageVisibilityRequestsSent, Is.Empty);
            Assert.That(result, Is.EqualTo(Renewal.Result.Stopped));
        });
    }
}