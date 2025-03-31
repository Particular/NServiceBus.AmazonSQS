namespace NServiceBus.Transport.SQS.Tests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        var sqsClient = new MockSqsClient();
        var fakeTimeProvider = new FakeTimeProvider();

        var expiresOn = fakeTimeProvider.Start.AddSeconds(10);

        var renewalTask = Renewal.RenewMessageVisibility(message, expiresOn, visibilityTimeoutInSeconds: 10, sqsClient, "inputQueue", fakeTimeProvider, tokenSource.Token);

        // advance time twice to simulate mor than one renewal
        fakeTimeProvider.Advance(TimeSpan.FromSeconds(5));
        fakeTimeProvider.Advance(TimeSpan.FromSeconds(5));

        await tokenSource.CancelAsync();

        await renewalTask;

        Assert.That(sqsClient.ChangeMessageVisibilityRequestsSent, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(sqsClient.ChangeMessageVisibilityRequestsSent.ElementAt(0).VisibilityTimeout, Is.EqualTo(15));
            Assert.That(sqsClient.ChangeMessageVisibilityRequestsSent.ElementAt(1).VisibilityTimeout, Is.EqualTo(15));
        });
    }

    [Test]
    public async Task Should_not_renew_when_time_passed_below_renewal_time()
    {
        var message = new Message();

        var tokenSource = new CancellationTokenSource();
        var sqsClient = new MockSqsClient();
        var fakeTimeProvider = new FakeTimeProvider();

        var expiresOn = fakeTimeProvider.Start.AddSeconds(30);

        var renewalTask = Renewal.RenewMessageVisibility(message, expiresOn, visibilityTimeoutInSeconds: 10, sqsClient, "inputQueue", fakeTimeProvider, tokenSource.Token);

        // advance time but still below renewal time
        fakeTimeProvider.Advance(TimeSpan.FromSeconds(18));

        await tokenSource.CancelAsync();

        await renewalTask;

        Assert.That(sqsClient.ChangeMessageVisibilityRequestsSent, Is.Empty);
    }

    [Test]
    public async Task Should_renew_when_expired_but_below_threshold()
    {
        var message = new Message();

        var tokenSource = new CancellationTokenSource();
        var sqsClient = new MockSqsClient();
        var fakeTimeProvider = new FakeTimeProvider();

        var expiresOn = fakeTimeProvider.Start.AddSeconds(-1);

        var renewalTask = Renewal.RenewMessageVisibility(message, expiresOn, visibilityTimeoutInSeconds: 10, sqsClient, "inputQueue", fakeTimeProvider, tokenSource.Token);

        // advance time slightly
        fakeTimeProvider.Advance(TimeSpan.FromMilliseconds(300));

        await tokenSource.CancelAsync();

        await renewalTask;

        Assert.That(sqsClient.ChangeMessageVisibilityRequestsSent, Has.Count.EqualTo(1));
        Assert.That(sqsClient.ChangeMessageVisibilityRequestsSent.ElementAt(0).VisibilityTimeout, Is.EqualTo(10));
    }

    [Test]
    public async Task Should_renew_when_expired_but_below_buffer()
    {
        var message = new Message();

        var tokenSource = new CancellationTokenSource();
        var sqsClient = new MockSqsClient();
        var fakeTimeProvider = new FakeTimeProvider();

        var expiresOn = fakeTimeProvider.Start.AddSeconds(160);

        var renewalTask = Renewal.RenewMessageVisibility(message, expiresOn, visibilityTimeoutInSeconds: 10, sqsClient, "inputQueue", fakeTimeProvider, tokenSource.Token);

        // advanced to renewal time
        fakeTimeProvider.Advance(TimeSpan.FromSeconds(150));

        await tokenSource.CancelAsync();

        await renewalTask;

        Assert.That(sqsClient.ChangeMessageVisibilityRequestsSent, Has.Count.EqualTo(1));
        Assert.That(sqsClient.ChangeMessageVisibilityRequestsSent.ElementAt(0).VisibilityTimeout, Is.EqualTo(20));
    }
}