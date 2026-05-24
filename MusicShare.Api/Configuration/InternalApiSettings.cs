using System.Security.Cryptography;
using System.Text;

namespace MusicShare.Api.Configuration;

public class InternalApiSettings
{
    public const string SectionName = "InternalApi";

    public string ApiKey { get; set; } = string.Empty;
    public string HeaderName { get; set; } = "X-Internal-API-Key";

    public bool IsAuthorized(string? providedKey)
    {
        if (string.IsNullOrWhiteSpace(ApiKey) || string.IsNullOrWhiteSpace(providedKey))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(ApiKey);
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);

        return expectedBytes.Length == providedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
