using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace MusicShare.Worker.Services
{
    public class FrontendRevalidateService(HttpClient client, ILogger<FrontendRevalidateService> logger) : IFrontendRevalidateService
    {
        private readonly HttpClient _client = client;
        private readonly ILogger<FrontendRevalidateService> _logger = logger;

        public async Task RevalidateAsync(RevalidateFrontendRequest request)
        {
            try
            {
                var response = await _client.PostAsJsonAsync("/api/revalidate", request);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation("Revalidation triggered for ShareId={ShareId}", request.ShareId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to trigger revalidation for ShareId={ShareId}", request.ShareId);
            }
        }
    }

    public record RevalidateFrontendRequest(string ShareId);
}
