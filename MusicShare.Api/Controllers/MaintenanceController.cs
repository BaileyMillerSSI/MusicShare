using MediatR;
using Microsoft.AspNetCore.Mvc;
using MusicShare.Api.Commands;
using MusicShare.Api.Security;

namespace MusicShare.Api.Controllers;

[ApiController]
[Route("internal/maintenance/duplicate-shares")]
[MaintenanceApiKey]
public sealed class MaintenanceController(IMediator mediator) : ControllerBase
{
    [HttpPost("reconcile")]
    public async Task<ActionResult<ReconcileDuplicateShares.Response>> Reconcile([FromBody] ReconcileDuplicateShares.Request request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(request, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(new { result.Error });
    }
}
