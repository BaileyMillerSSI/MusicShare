using MediatR;
using Microsoft.AspNetCore.Mvc;
using MusicShare.Api.Queries;
using MusicShare.Api.Security;

namespace MusicShare.Api.Controllers;

[ApiController]
[Route("internal")]
[InternalApiKey]
public class InternalController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Get all share IDs for completed share requests.
    /// </summary>
    [HttpGet("share/ids")]
    public async Task<ActionResult<GetAllShareIds.Result>> GetAllShareIds(
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAllShareIds.Query(), cancellationToken);

        return Ok(result);
    }
}
