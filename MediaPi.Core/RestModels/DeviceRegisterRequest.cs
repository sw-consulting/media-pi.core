// Developed by Maxim [maxirmx] Samsonov (www.sw.consulting)
// This file is a part of Media Pi backend application

namespace MediaPi.Core.RestModels;

public sealed class DeviceRegisterRequest
{
    public string PublicKeyOpenSsh { get; set; } = "";
    public string? SshUser { get; set; }
}
