using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace MusicShare.Services.Services
{
    public class FrontendRevalidateService(HttpClient client, ILogger<FrontendRevalidateService> logger) : IFrontendRevalidateService
    {
        private readonly HttpClient _client = client;
        private readonly ILogger<FrontendRevalidateService> _logger = logger;

        public Task<bool> RevalidateShareAsync(string shareId, CancellationToken cancellationToken = default) => RevalidateAsync(new { shareId }, $"ShareId={shareId}", cancellationToken: cancellationToken);

        public Task<bool> RevalidateMetricsAsync(CancellationToken cancellationToken = default) => RevalidateAsync(new { target = "metrics" }, "metrics", returnSuccess: true, cancellationToken);

        private async Task<bool> RevalidateAsync(object request, string target, bool returnSuccess = true, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _client.PostAsJsonAsync("/api/revalidate", request, cancellationToken);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation("Revalidation triggered for {Target}", target);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to trigger revalidation for {Target}", target);
                return false;
            }
        }
    }

}
