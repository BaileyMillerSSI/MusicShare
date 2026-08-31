using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace MusicShare.Services.Services
{
    public class FrontendRevalidateService(HttpClient client, ILogger<FrontendRevalidateService> logger) : IFrontendRevalidateService
    {
        private readonly HttpClient _client = client;
        private readonly ILogger<FrontendRevalidateService> _logger = logger;

        public Task RevalidateShareAsync(string shareId) => RevalidateAsync(new { shareId }, $"ShareId={shareId}");

        public Task RevalidateMetricsAsync() => RevalidateAsync(new { target = "metrics" }, "metrics");

        private async Task RevalidateAsync(object request, string target)
        {
            try
            {
                var response = await _client.PostAsJsonAsync("/api/revalidate", request);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation("Revalidation triggered for {Target}", target);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to trigger revalidation for {Target}", target);
            }
        }
    }

}
