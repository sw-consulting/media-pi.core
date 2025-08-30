using System.Collections.Generic;

namespace MediaPi.Core.DTOs;

public sealed class DeviceRegisterRequest
{
    public string PublicKeyOpenSsh { get; set; } = "";
    public string? Hostname { get; set; }
    public string? Os { get; set; }
    public string? SshUser { get; set; }
    public string? Version { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}
