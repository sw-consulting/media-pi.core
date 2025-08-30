using System.Collections.Generic;

namespace MediaPi.Core.RestModels;

public sealed class DeviceRegisterRequest
{
    public string PublicKeyOpenSsh { get; set; } = "";
    public string? SshUser { get; set; }
    public string? Version { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}
