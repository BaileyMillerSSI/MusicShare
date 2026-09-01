namespace MusicShare.Api.Security;

public sealed class MaintenanceSettings
{
    public const string SectionName = "Maintenance";
    public string Secret { get; set; } = string.Empty;
}
