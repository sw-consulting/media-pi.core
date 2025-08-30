using System.ComponentModel.DataAnnotations;

namespace MediaPi.Core.Data.Entities;

public class Device
{
    [Key] public string Id { get; set; } = "";            // deviceId
    [Required] public string PublicKeyOpenSsh { get; set; } = "";
    public string? Hostname { get; set; }
    public string? Os { get; set; }
    public string SshUser { get; set; } = "pi";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    // Optional: JSONB Tags, Version, LastSeen, etc.
}
