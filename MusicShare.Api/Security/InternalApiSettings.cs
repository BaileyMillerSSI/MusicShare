namespace MusicShare.Api.Security;

public sealed class InternalApiSettings
{
    public const string SectionName = "InternalApi";
    public const string SecretHeaderName = "X-MusicShare-Internal-Secret";

    public string? SharedSecret { get; set; }
}
