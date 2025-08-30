using MediaPi.Core.Models;

namespace MediaPi.Core.RestModels;

public sealed class DeviceRegisterResponse
{
    public string DeviceId { get; set; } = "";
    public string Alias => $"pi-{DeviceId}";
    public string SocketPath  => $"/run/mediapi/{DeviceId}.ssh.sock";
}
