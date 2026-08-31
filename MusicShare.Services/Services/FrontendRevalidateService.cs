using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace MusicShare.Services.Services
{
    public class FrontendRevalidateService(HttpClient client, ILogger<FrontendRevalidateService> logger) : IFrontendRevalidateService
    {
        private readonly HttpClient _client = client;
        private readonly ILogger<FrontendRevalidateService> _logger = logger;

        public Task RevalidateShareAsync(string shareId) => RevalidateAsync(new { shareId }, $"ShareId={shareId}");

        public Task<bool> RevalidateMetricsAsync() => RevalidateAsync(new { target = "metrics" }, "metrics", returnSuccess: true);

        private async Task RevalidateAsync(object request, string target)
        {
            await RevalidateAsync(request, target, returnSuccess: false);
        }

        private async Task<bool> RevalidateAsync(object request, string target, bool returnSuccess = true)
        {
            try
            {
                var response = await _client.PostAsJsonAsync("/api/revalidate", request);
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
