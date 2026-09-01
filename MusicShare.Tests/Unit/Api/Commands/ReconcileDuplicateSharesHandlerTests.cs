using MassTransit;
using Moq;
using MusicShare.Api.Commands;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Services;

namespace MusicShare.Tests.Unit.Api.Commands;

public class ReconcileDuplicateSharesHandlerTests
{
    [Fact]
    public async Task ItWillNotPublishMetricsWhenCanonicalRevalidationFails()
    {
        var reconciliation = new Mock<IDuplicateShareReconciliationService>();
        var revalidation = new Mock<IFrontendRevalidateService>();
        var publish = new Mock<IPublishEndpoint>();
        reconciliation.Setup(x => x.ReconcileAsync(It.IsAny<DuplicateShareReconciliationRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result());
        revalidation.Setup(x => x.RevalidateShareAsync("aaaaaaaaaaaa", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        revalidation.Setup(x => x.RevalidateShareAsync("bbbbbbbbbbbb", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var result = await new ReconcileDuplicateShares.Handler(reconciliation.Object, revalidation.Object, publish.Object).Handle(Request(), CancellationToken.None);
        result.Success.Should().BeFalse();
        result.Changed.Should().BeTrue("the alias was durably written before revalidation failed");
        result.AffectedShareCount.Should().Be(2);
        result.OperationId.Should().Be("operation");
        result.CanonicalShareId.Should().Be("aaaaaaaaaaaa");
        result.AliasShareId.Should().Be("bbbbbbbbbbbb");
        publish.Verify(x => x.Publish(It.IsAny<RefreshPublicMetrics>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ItWillNotPublishMetricsWhenAliasRevalidationFails()
    {
        var reconciliation = new Mock<IDuplicateShareReconciliationService>(); var revalidation = new Mock<IFrontendRevalidateService>(); var publish = new Mock<IPublishEndpoint>();
        reconciliation.Setup(x => x.ReconcileAsync(It.IsAny<DuplicateShareReconciliationRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result());
        revalidation.Setup(x => x.RevalidateShareAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var result = await new ReconcileDuplicateShares.Handler(reconciliation.Object, revalidation.Object, publish.Object).Handle(Request(), CancellationToken.None);
        result.Success.Should().BeFalse(); publish.Verify(x => x.Publish(It.IsAny<RefreshPublicMetrics>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ItWillPublishMetricsOnlyAfterBothRevalidationsSucceed()
    {
        var reconciliation = new Mock<IDuplicateShareReconciliationService>(); var revalidation = new Mock<IFrontendRevalidateService>(); var publish = new Mock<IPublishEndpoint>();
        reconciliation.Setup(x => x.ReconcileAsync(It.IsAny<DuplicateShareReconciliationRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result(changed: false));
        revalidation.Setup(x => x.RevalidateShareAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        publish.Setup(x => x.Publish(It.IsAny<RefreshPublicMetrics>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var result = await new ReconcileDuplicateShares.Handler(reconciliation.Object, revalidation.Object, publish.Object).Handle(Request(), CancellationToken.None);
        result.Success.Should().BeTrue(); result.AffectedShareCount.Should().Be(2);
        publish.Verify(x => x.Publish(It.IsAny<RefreshPublicMetrics>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ReconcileDuplicateShares.Request Request() => new("aaaaaaaaaaaa", "bbbbbbbbbbbb", null, "apply", "f");
    private static DuplicateShareReconciliationResult Result(bool changed = true) => new(true, changed, null, "operation", "f", "aaaaaaaaaaaa", "bbbbbbbbbbbb", []);
}
