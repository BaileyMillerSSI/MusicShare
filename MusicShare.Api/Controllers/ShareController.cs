using MassTransit;
using Microsoft.AspNetCore.Mvc;
using MusicShare.Api.Models;
using MusicShare.Api.Services;
using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Persistence.Entities;
using MusicShare.Persistence.Repositories;

namespace MusicShare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShareController : ControllerBase
{
    private readonly ILogger<ShareController> _logger;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly ISongRepository _songRepository;
    private readonly ISongServiceLinkRepository _linkRepository;
    private readonly UrlNormalizer _urlNormalizer;

    public ShareController(
        ILogger<ShareController> logger,
        IPublishEndpoint publishEndpoint,
        IShareRequestRepository shareRequestRepository,
        ISongRepository songRepository,
        ISongServiceLinkRepository linkRepository,
        UrlNormalizer urlNormalizer)
    {
        _logger = logger;
        _publishEndpoint = publishEndpoint;
        _shareRequestRepository = shareRequestRepository;
        _songRepository = songRepository;
        _linkRepository = linkRepository;
        _urlNormalizer = urlNormalizer;
    }

    /// <summary>
    /// Submit a music URL to be resolved across services.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SubmitShareResponse>> SubmitShare(
        [FromBody] SubmitShareRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received share submission for URL: {Url}", request.Url);

        // Detect service type
        var serviceType = _urlNormalizer.DetectServiceType(request.Url);
        if (serviceType == ServiceType.Unknown)
        {
            return BadRequest(new { error = "Unsupported music service URL" });
        }

        // Generate share ID
        var shareId = _urlNormalizer.GenerateShareId();
        var correlationId = Guid.NewGuid();

        // Create ShareRequest entity
        var shareRequest = new ShareRequest
        {
            ShareId = shareId,
            SourceUrl = request.Url,
            SourceService = serviceType,
            Status = ShareStatus.Pending,
            CorrelationId = correlationId,
            CreatedAt = DateTime.UtcNow
        };

        await _shareRequestRepository.InsertAsync(shareRequest, cancellationToken);

        _logger.LogInformation(
            "Created ShareRequest: ShareId={ShareId}, ServiceType={ServiceType}",
            shareId, serviceType);

        // Publish message to trigger async processing
        await _publishEndpoint.Publish(new SongShareSubmitted
        {
            ShareId = shareId,
            SourceUrl = request.Url,
            SourceService = serviceType,
            CorrelationId = correlationId
        }, cancellationToken);

        _logger.LogInformation("Published SongShareSubmitted for ShareId={ShareId}", shareId);

        return Ok(new SubmitShareResponse
        {
            ShareId = shareId,
            Status = ShareStatus.Pending.ToString()
        });
    }

    /// <summary>
    /// Get the results of a share request by ShareId.
    /// </summary>
    [HttpGet("{shareId}")]
    public async Task<ActionResult<ShareResultResponse>> GetShareResult(
        string shareId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching share result for ShareId={ShareId}", shareId);

        // Get the ShareRequest
        var shareRequest = await _shareRequestRepository.GetByShareIdAsync(shareId, cancellationToken);
        if (shareRequest == null)
        {
            return NotFound(new { error = "Share not found" });
        }

        var response = new ShareResultResponse
        {
            ShareId = shareRequest.ShareId,
            Status = shareRequest.Status.ToString(),
            Song = null
        };

        // If we have a SongId, fetch the song details
        if (!string.IsNullOrEmpty(shareRequest.SongId))
        {
            var song = await _songRepository.GetByIdAsync(shareRequest.SongId, cancellationToken);
            if (song != null)
            {
                var links = await _linkRepository.GetBySongIdAsync(song.Id, cancellationToken);

                response = response with
                {
                    Song = new SongDetails
                    {
                        Id = song.Id,
                        Title = song.Title,
                        Artists = song.Artists,
                        Album = song.Album,
                        ArtworkUrl = song.ArtworkUrl,
                        Duration = song.Duration,
                        Status = song.Status.ToString(),
                        Links = links.Select(l => new ServiceLink
                        {
                            ServiceType = l.ServiceType,
                            Url = l.NormalizedUrl
                        }).ToList()
                    }
                };
            }
        }

        return Ok(response);
    }
}
