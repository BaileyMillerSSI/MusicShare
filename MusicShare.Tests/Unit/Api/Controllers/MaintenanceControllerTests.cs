using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MusicShare.Api.Commands;
using MusicShare.Api.Controllers;

namespace MusicShare.Tests.Unit.Api.Controllers;

public sealed class MaintenanceControllerTests
{
    [Fact]
    public async Task ItWillReturnOnlyTheBoundedCommandResponseForSuccessfulReconciliation()
    {
        var mediator = new Mock<IMediator>();
        var response = new ReconcileDuplicateShares.Response(true, true, null, "reconcile-fingerprint", "fingerprint", "aaaaaaaaaaaa", "bbbbbbbbbbbb", [], 2);
        mediator.Setup(x => x.Send(It.IsAny<ReconcileDuplicateShares.Request>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await new MaintenanceController(mediator.Object).Reconcile(new("aaaaaaaaaaaa", "bbbbbbbbbbbb", null, "dry-run", null), CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(response);
        mediator.Verify(x => x.Send(It.IsAny<ReconcileDuplicateShares.Request>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ItWillNotProjectInternalFailureFields()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(x => x.Send(It.IsAny<ReconcileDuplicateShares.Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReconcileDuplicateShares.Response.Failure("fixed failure"));

        var result = await new MaintenanceController(mediator.Object).Reconcile(new("aaaaaaaaaaaa", "bbbbbbbbbbbb", null, "invalid", null), CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }
}
