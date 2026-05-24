using MediatR;
using Microsoft.AspNetCore.Mvc;
using MusicShare.Api.Commands;
using MusicShare.Api.Queries;

namespace MusicShare.Api.Controllers;

[ApiController]
[Route("internal")]
public class InternalController(
    IMediator mediator,
    IConfiguration configuration) : ControllerBase
{
    private readonly IMediator _mediator = mediator;
    private readonly IConfiguration _configuration = configuration;

    /// <summary>
    /// Get all share IDs for completed share requests.
    /// This endpoint is not proxied by the frontend, so it is only reachable internally.
    /// </summary>
    [HttpGet("share/ids")]
    public async Task<ActionResult<GetAllShareIds.Result>> GetAllShareIds(
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAllShareIds.Query(), cancellationToken);

        return Ok(result);
    }

    [HttpPost("reindex/all")]
    public async Task<ActionResult<ReindexAllShares.Response>> ReindexAll(
        CancellationToken cancellationToken)
    {
        var authResult = ValidateReindexApiKey();
        if (authResult != null)
        {
            return authResult;
        }

        var result = await _mediator.Send(new ReindexAllShares.Request(), cancellationToken);

        return Ok(result);
    }

    [HttpPost("reindex/song/{songId}")]
    public async Task<ActionResult<ReindexSong.Response>> ReindexSong(
        [FromRoute] string songId,
        CancellationToken cancellationToken)
    {
        var authResult = ValidateReindexApiKey();
        if (authResult != null)
        {
            return authResult;
        }

        if (!IsObjectId(songId))
        {
            return BadRequest(new { error = "songId must be a 24-character hexadecimal ObjectId" });
        }

        var result = await _mediator.Send(new ReindexSong.Request(songId), cancellationToken);
        if (!result.Found)
        {
            return NotFound(new { error = "Song not found" });
        }

        return Ok(result);
    }

    private ActionResult? ValidateReindexApiKey()
    {
        var secret = _configuration["REINDEX_API_KEY"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Re-index API key not configured" });
        }

        if (Request.Headers["X-API-KEY"] != secret)
        {
            return Unauthorized(new { error = "Unauthorized" });
        }

        return null;
    }

    private static bool IsObjectId(string value) =>
        value.Length == 24 && value.All(Uri.IsHexDigit);
}
