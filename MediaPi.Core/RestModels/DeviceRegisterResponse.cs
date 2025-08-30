// Developed by Maxim [maxirmx] Samsonov (www.sw.consulting)
// This file is a part of Media Pi backend application

namespace MediaPi.Core.RestModels;

public sealed class DeviceRegisterResponse : Reference
{
    public string Alias => $"pi-{Id}";
    public string SocketPath => $"/run/mediapi/{Id}.ssh.sock";
}
