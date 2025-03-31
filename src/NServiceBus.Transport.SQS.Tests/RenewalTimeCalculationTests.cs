namespace NServiceBus.Transport.SQS.Tests;

using System;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

[TestFixture]
public class RenewalTimeCalculationTests
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

        var result = RenewalTimeCalculation.Calculate(visibilityTimeExpiresOn, fakeTimeProvider);

        Assert.That(result, Is.EqualTo(TimeSpan.FromSeconds(expected)));
    }

    [Test]
    public void Should_return_zero_delay_for_remaining_time_smaller_than_minimum_threshold()
    {
        var fixedTime = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var fakeTimeProvider = new FakeTimeProvider(fixedTime);
        var visibilityTimeExpiresOn = fixedTime.AddMilliseconds(300);

        var result = RenewalTimeCalculation.Calculate(visibilityTimeExpiresOn, fakeTimeProvider);

        Assert.That(result, Is.EqualTo(TimeSpan.Zero));
    }
}