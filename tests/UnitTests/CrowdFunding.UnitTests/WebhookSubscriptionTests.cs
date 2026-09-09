using CrowdFunding.Modules.CampaignUpdates.Application.Security;
using CrowdFunding.Modules.CampaignUpdates.Domain.Aggregates;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Security;

namespace CrowdFunding.UnitTests;

/// <summary>
/// TICKET-035 acceptance criterion #1: SSRF/private-network rejection at webhook registration.
/// </summary>
public sealed class UrlSecurityValidatorTests
{
    [Fact]
    public void IsSafeExternalUrl_ShouldRejectLoopback()
        => Assert.False(UrlSecurityValidator.IsSafeExternalUrl(new Uri("https://127.0.0.1/webhook")));

    [Fact]
    public void IsSafeExternalUrl_ShouldRejectCloudMetadataAddress()
        => Assert.False(UrlSecurityValidator.IsSafeExternalUrl(new Uri("https://169.254.169.254/latest/meta-data/")));

    [Theory]
    [InlineData("https://10.0.0.5/webhook")]
    [InlineData("https://172.16.0.5/webhook")]
    [InlineData("https://192.168.1.5/webhook")]
    public void IsSafeExternalUrl_ShouldRejectPrivateRanges(string url)
        => Assert.False(UrlSecurityValidator.IsSafeExternalUrl(new Uri(url)));

    [Fact]
    public void IsSafeExternalUrl_ShouldRejectNonHttps()
        => Assert.False(UrlSecurityValidator.IsSafeExternalUrl(new Uri("http://example.com/webhook")));

    [Fact]
    public void IsSafeExternalUrl_ShouldAcceptAPublicHttpsAddress()
        => Assert.True(UrlSecurityValidator.IsSafeExternalUrl(new Uri("https://example.com/webhook")));
}

/// <summary>TICKET-035: the outbound signature format, and the delivery-health state machine.</summary>
public sealed class WebhookPayloadSignerTests
{
    [Fact]
    public void Sign_ShouldProduceATimestampedHmacSignature()
    {
        var signature = WebhookPayloadSigner.Sign("""{"eventType":"pledge.confirmed"}""", "secret", DateTimeOffset.UtcNow);

        Assert.Matches(@"^t=\d+,v1=[0-9a-f]{64}$", signature);
    }

    [Fact]
    public void Sign_ShouldProduceDifferentSignatures_ForDifferentPayloads()
    {
        var now = DateTimeOffset.UtcNow;
        var first = WebhookPayloadSigner.Sign("""{"a":1}""", "secret", now);
        var second = WebhookPayloadSigner.Sign("""{"a":2}""", "secret", now);

        Assert.NotEqual(first, second);
    }
}

public sealed class WebhookSubscriptionDeliveryHealthTests
{
    [Fact]
    public void RecordDeliveryFailure_ShouldDisableTheSubscription_AfterReachingTheThreshold()
    {
        var subscription = WebhookSubscription.Create(Guid.NewGuid(), "https://example.com/hook", "secret", DateTime.UtcNow);

        for (var i = 0; i < 4; i++)
        {
            subscription.RecordDeliveryFailure(maxConsecutiveFailures: 5);
            Assert.True(subscription.IsActive);
        }

        subscription.RecordDeliveryFailure(maxConsecutiveFailures: 5);

        Assert.False(subscription.IsActive);
    }

    [Fact]
    public void RecordDeliverySuccess_ShouldResetTheConsecutiveFailureCounter()
    {
        var subscription = WebhookSubscription.Create(Guid.NewGuid(), "https://example.com/hook", "secret", DateTime.UtcNow);
        subscription.RecordDeliveryFailure(maxConsecutiveFailures: 5);
        subscription.RecordDeliveryFailure(maxConsecutiveFailures: 5);

        subscription.RecordDeliverySuccess();
        for (var i = 0; i < 4; i++)
        {
            subscription.RecordDeliveryFailure(maxConsecutiveFailures: 5);
        }

        // Had the earlier failures not been reset, this 4th-since-reset failure would already
        // have disabled the subscription.
        Assert.True(subscription.IsActive);
    }
}

public sealed class WebhookDeliveryTaskTests
{
    [Fact]
    public void MarkFailed_ShouldGoDead_AfterExhaustingTheBackoffSchedule()
    {
        var task = WebhookDeliveryTask.Create(Guid.NewGuid(), "pledge.confirmed", "{}", DateTime.UtcNow);

        for (var i = 0; i < 4; i++)
        {
            task.MarkFailed("timeout", DateTime.UtcNow);
            Assert.Equal(WebhookDeliveryStatus.Pending, task.Status);
        }

        task.MarkFailed("timeout", DateTime.UtcNow);

        Assert.Equal(WebhookDeliveryStatus.Dead, task.Status);
        Assert.Equal("timeout", task.LastError);
    }

    [Fact]
    public void MarkFailed_ShouldBackOffTheScheduledTime()
    {
        var now = DateTime.UtcNow;
        var task = WebhookDeliveryTask.Create(Guid.NewGuid(), "pledge.confirmed", "{}", now);

        task.MarkFailed("timeout", now);

        Assert.True(task.ScheduledAtUtc > now);
    }
}
