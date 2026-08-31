using Microsoft.AspNetCore.Mvc;
using MusicShare.Services.Models;
using MusicShare.Services.Services;

namespace MusicShare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetricsController(IPublicMetricsService metrics) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PublicMetricsResponse>> Get(CancellationToken cancellationToken) =>
        Ok(await metrics.GetAsync(cancellationToken));
}
