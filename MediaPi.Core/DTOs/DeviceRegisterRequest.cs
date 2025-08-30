using System.Collections.Generic;

namespace MediaPi.Core.DTOs;

public sealed class DeviceRegisterRequest
{
    public string PublicKeyOpenSsh { get; set; } = "";
    public string? HostName { get; set; }
    public string? OperatingSystem { get; set; }
    public string? SshUser { get; set; }
    public string? Version { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}
