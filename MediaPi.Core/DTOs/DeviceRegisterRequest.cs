namespace MediaPi.Core.DTOs;

public sealed class DeviceRegisterRequest
{
    public string PublicKeyOpenSsh { get; set; } = "";  // e.g., "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAI..."
    public string? Hostname { get; set; }
    public string? Os { get; set; }
    public string? SshUser { get; set; }   // default "pi"
    public string? Version { get; set; }   // optional device agent version
    public Dictionary<string,string>? Tags { get; set; } // optional labels
}
