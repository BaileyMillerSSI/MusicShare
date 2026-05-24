using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MusicShare.Api.Configuration;
using MusicShare.Api.Queries;

namespace MusicShare.Api.Controllers;

[ApiController]
[Route("internal")]
public class InternalController(
    IMediator mediator,
    IOptions<InternalApiSettings> internalApiOptions) : ControllerBase
{
    private readonly IMediator _mediator = mediator;
    private readonly InternalApiSettings _internalApiSettings = internalApiOptions.Value;

    /// <summary>
    /// Get all share IDs for completed share requests.
    /// </summary>
    [HttpGet("share/ids")]
    public async Task<ActionResult<GetAllShareIds.Result>> GetAllShareIds(
        CancellationToken cancellationToken)
    {
        var providedApiKey = Request.Headers[_internalApiSettings.HeaderName].FirstOrDefault();
        if (!_internalApiSettings.IsAuthorized(providedApiKey))
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(new GetAllShareIds.Query(), cancellationToken);

        return Ok(result);
    }
}
