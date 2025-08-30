// Developed by Maxim [maxirmx] Samsonov (www.sw.consulting)
// This file is a part of Media Pi backend application

namespace MediaPi.Core.RestModels;

public sealed class DeviceRegisterResponse: Reference
{
    public string PiDeviceId { get; init; } = "noname";
    public string Alias => $"pi-{PiDeviceId}";
    public string SocketPath => $"/run/mediapi/{PiDeviceId}.ssh.sock";
}
