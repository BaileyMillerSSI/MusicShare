using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MusicShare.Api.Services;
using MusicShare.Contracts.Messages;

namespace MusicShare.Tests.Unit.Api.Services;

public class PublicMetricsDailyRefreshServiceTests
{
    [Theory]
    [InlineData(2026, 1, 10, 23, 59, 0, 0, 0, 1)]
    [InlineData(2026, 1, 11, 0, 0, 0, 1, 0, 0)]
    public void ItWillWaitUntilTheNextMidnightUtcBoundary(
        int year, int month, int day, int hour, int minute, int second,
        int expectedDays, int expectedHours, int expectedMinutes)
    {
        var now = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);

        PublicMetricsDailyRefreshService.GetDelayUntilNextMidnightUtc(now)
            .Should().Be(new TimeSpan(expectedDays, expectedHours, expectedMinutes, 0));
    }

    [Fact]
    public async Task ItWillPublishARefreshAtMidnightWithoutANewSong()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var now = new DateTime(2026, 1, 10, 23, 59, 0, DateTimeKind.Utc);
        var delays = 0;
        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publish = new Mock<IPublishEndpoint>();
        publish.Setup(x => x.Publish(It.IsAny<RefreshPublicMetrics>(), It.IsAny<CancellationToken>()))
            .Callback(() => published.TrySetResult())
            .Returns(Task.CompletedTask);
        var service = CreateSut(publish.Object, () => now, (delay, token) =>
        {
            if (Interlocked.Increment(ref delays) == 1)
            {
                delay.Should().Be(TimeSpan.FromMinutes(1));
                now = now.Add(delay);
                return Task.CompletedTask;
            }

            return Task.Delay(Timeout.InfiniteTimeSpan, token);
        });

        await service.StartAsync(cancellationToken);
        await published.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        await service.StopAsync(cancellationToken);

        publish.Verify(x => x.Publish(It.IsAny<RefreshPublicMetrics>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ItWillRetryTheSameDailyRefreshAfterAPublishFailure()
    {
        var now = new DateTime(2026, 1, 10, 23, 59, 0, DateTimeKind.Utc);
        var delays = new List<TimeSpan>();
        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publishAttempts = 0;
        var publish = new Mock<IPublishEndpoint>();
        publish.Setup(x => x.Publish(It.IsAny<RefreshPublicMetrics>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (Interlocked.Increment(ref publishAttempts) == 1)
                    return Task.FromException(new InvalidOperationException("Transient publish failure"));

                published.TrySetResult();
                return Task.CompletedTask;
            });
        var service = CreateSut(publish.Object, () => now, (delay, token) =>
        {
            delays.Add(delay);
            if (delays.Count == 1)
            {
                now = now.Add(delay);
                return Task.CompletedTask;
            }

            if (delays.Count == 2)
            {
                delay.Should().Be(TimeSpan.FromSeconds(5));
                return Task.CompletedTask;
            }

            return Task.Delay(Timeout.InfiniteTimeSpan, token);
        }, TimeSpan.FromSeconds(5));

        await service.StartAsync(TestContext.Current.CancellationToken);
        await published.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        publish.Verify(x => x.Publish(It.IsAny<RefreshPublicMetrics>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ItWillStopCleanlyWhenCancelledDuringAPublishRetryDelay()
    {
        var now = new DateTime(2026, 1, 10, 23, 59, 0, DateTimeKind.Utc);
        var retryDelayStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publish = new Mock<IPublishEndpoint>();
        publish.Setup(x => x.Publish(It.IsAny<RefreshPublicMetrics>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Transient publish failure"));
        var service = CreateSut(publish.Object, () => now, async (delay, token) =>
        {
            if (delay == TimeSpan.FromMinutes(1))
            {
                now = now.Add(delay);
                return;
            }

            retryDelayStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }, TimeSpan.FromSeconds(5));

        await service.StartAsync(TestContext.Current.CancellationToken);
        await retryDelayStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        publish.Verify(x => x.Publish(It.IsAny<RefreshPublicMetrics>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ItWillRejectNonUtcRolloverTimes()
    {
        Action action = () => PublicMetricsDailyRefreshService.GetDelayUntilNextMidnightUtc(DateTime.Now);

        action.Should().Throw<ArgumentException>();
    }

    private static PublicMetricsDailyRefreshService CreateSut(
        IPublishEndpoint publish,
        Func<DateTime> utcNow,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        TimeSpan? retryDelay = null)
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(IPublishEndpoint))).Returns(publish);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(provider.Object);
        var scopes = new Mock<IServiceScopeFactory>();
        scopes.Setup(x => x.CreateScope()).Returns(scope.Object);
        return new PublicMetricsDailyRefreshService(
            scopes.Object,
            Mock.Of<ILogger<PublicMetricsDailyRefreshService>>(),
            utcNow,
            delayAsync,
            retryDelay);
    }
}
