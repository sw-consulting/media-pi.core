namespace MediaPi.Core.DTOs;

public sealed class DeviceRegisterResponse
{
    public string DeviceId { get; set; } = "";
    public string Alias { get; set; } = "";       // e.g., "pi-fp-abc..."
    public string SocketPath { get; set; } = "";  // e.g., "/run/mediapi/fp-abc....ssh.sock"
}
