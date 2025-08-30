namespace MediaPi.Core.DTOs;

public sealed class DeviceRegisterResponse
{
    public string DeviceId { get; set; } = "";
    public string Alias { get; set; } = "";
    public string SocketPath { get; set; } = "";
}
