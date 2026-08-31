using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MusicShare.Api.Services;
using MusicShare.Services.Services;

namespace MusicShare.Tests.Unit.Api.Services;

public class PublicMetricsInvalidationRetryServiceTests
{
    [Fact]
    public async Task ItWillCoalesceRetriesAndContinueUntilInvalidationSucceeds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var attempts = 0;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var revalidate = new Mock<IFrontendRevalidateService>();
        revalidate.Setup(x => x.RevalidateMetricsAsync(It.IsAny<CancellationToken>())).Returns<CancellationToken>(_ =>
        {
            if (Interlocked.Increment(ref attempts) >= 3) completed.TrySetResult();
            return Task.FromResult(attempts >= 3);
        });
        var sut = CreateSut(revalidate.Object);

        await sut.StartAsync(cancellationToken);
        sut.ScheduleRetry();
        sut.ScheduleRetry();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        await sut.StopAsync(cancellationToken);

        attempts.Should().Be(3, "the capacity-one queue coalesces duplicate scheduling while retries remain pending");
    }

    [Fact]
    public async Task ItWillStopWithoutStartingAnotherAttemptWhenCancelled()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var revalidate = new Mock<IFrontendRevalidateService>();
        revalidate.Setup(x => x.RevalidateMetricsAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken token) => await Task.Delay(Timeout.InfiniteTimeSpan, token).ContinueWith(_ => false));
        var sut = CreateSut(revalidate.Object, TimeSpan.Zero);

        await sut.StartAsync(cancellationToken);
        sut.ScheduleRetry();
        await Task.Delay(20, cancellationToken);
        await sut.StopAsync(cancellationToken);

        revalidate.Verify(x => x.RevalidateMetricsAsync(It.IsAny<CancellationToken>()), Times.AtMostOnce);
    }

    private static PublicMetricsInvalidationRetryService CreateSut(IFrontendRevalidateService revalidate, TimeSpan? delay = null)
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(IFrontendRevalidateService))).Returns(revalidate);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(provider.Object);
        var scopes = new Mock<IServiceScopeFactory>();
        scopes.Setup(x => x.CreateScope()).Returns(scope.Object);
        return new PublicMetricsInvalidationRetryService(scopes.Object, Mock.Of<ILogger<PublicMetricsInvalidationRetryService>>(), delay ?? TimeSpan.Zero);
    }
}
