using MassTransit;
using Microsoft.AspNetCore.Mvc;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Models;
using MusicShare.Services.Services;

namespace MusicShare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetricsController(IPublicMetricsService metrics, IPublishEndpoint publishEndpoint) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PublicMetricsResponse>> Get(CancellationToken cancellationToken) =>
        Ok(await metrics.GetAsync(cancellationToken));

    [HttpPost("refresh")]
    public async Task<AcceptedResult> Refresh(CancellationToken cancellationToken)
    {
        await publishEndpoint.Publish(new RefreshPublicMetrics(), cancellationToken);

        return Accepted();
    }
}
