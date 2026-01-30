using System.ComponentModel.DataAnnotations;

namespace MusicShare.Api.Models;

public class SubmitShareRequest
{
    [Required]
    [Url]
    public string Url { get; set; } = string.Empty;
}
