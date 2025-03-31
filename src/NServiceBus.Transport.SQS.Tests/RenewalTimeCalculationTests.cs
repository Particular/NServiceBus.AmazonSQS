namespace NServiceBus.Transport.SQS.Tests;

using System;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

[TestFixture]
public class RenewalTimeCalculationTests
{
    [Test]
    public void Calculate_WhenRemainingTimeIsOneSecond_ReturnsHalfSecondDelay()
    {
        var fixedTime = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var fakeTimeProvider = new FakeTimeProvider(fixedTime);
        var visibilityTimeExpiresOn = fixedTime.AddSeconds(1);

        var result = RenewalTimeCalculation.Calculate(visibilityTimeExpiresOn, fakeTimeProvider);

        Assert.That(result, Is.EqualTo(TimeSpan.FromSeconds(0.5)));
    }

    [Test]
    public void Calculate_WhenRemainingTimeIsTwentySeconds_ReturnsTenSecondsDelay()
    {
        var fixedTime = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var fakeTimeProvider = new FakeTimeProvider(fixedTime);
        var visibilityTimeExpiresOn = fixedTime.AddSeconds(20);

        var result = RenewalTimeCalculation.Calculate(visibilityTimeExpiresOn, fakeTimeProvider);

        Assert.That(result, Is.EqualTo(TimeSpan.FromSeconds(10)));
    }

    [Test]
    public void Calculate_WhenRemainingTimeIsLessThan400Milliseconds_ReturnsZeroDelay()
    {
        var fixedTime = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var fakeTimeProvider = new FakeTimeProvider(fixedTime);
        var visibilityTimeExpiresOn = fixedTime.AddMilliseconds(300);

        var result = RenewalTimeCalculation.Calculate(visibilityTimeExpiresOn, fakeTimeProvider);

        Assert.That(result, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void Calculate_WhenRemainingTimeIsOneHundredSixtySeconds_ReturnsOneHundredFiftySecondsDelay()
    {
        var fixedTime = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var fakeTimeProvider = new FakeTimeProvider(fixedTime);
        var visibilityTimeExpiresOn = fixedTime.AddSeconds(160);

        var result = RenewalTimeCalculation.Calculate(visibilityTimeExpiresOn, fakeTimeProvider);

        Assert.That(result, Is.EqualTo(TimeSpan.FromSeconds(150)));
    }
}