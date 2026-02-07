using MediatR;
using Microsoft.AspNetCore.Mvc;
using MusicShare.Api.Commands;
using MusicShare.Api.Queries;
using MusicShare.Services.Models;

namespace MusicShare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShareController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Submit a music URL to be resolved across services.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SubmitShareResponse>> SubmitShare(
        [FromBody] SubmitShare.Request command,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new SubmitShareResponse
        {
            ShareId = result.ShareId!,
            Status = result.Status!
        });
    }

    /// <summary>
    /// Get all share IDs for completed share requests.
    /// Used by the frontend to pre-cache ISR routes at build time.
    /// </summary>
    [HttpGet("ids")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetAllShareIds(
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAllShareIds.Query(), cancellationToken);

        return Ok(result.ShareIds);
    }

    /// <summary>
    /// Get the results of a share request by ShareId.
    /// </summary>
    [HttpGet("{shareId}")]
    public async Task<ActionResult<ShareResultResponse>> GetShareResult(
        [FromRoute] string shareId,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        var result = await _mediator.Send(new GetShareResult.Query(shareId), cancellationToken);

        if (!result.Found)
        {
            return NotFound(new { error = "Share not found" });
        }

        return Ok(result.Response);
    }
}
