// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.RestModels;

public sealed class DeviceRegisterRequest
{
    public string? ServerKey { get; set; }
    public string? Name { get; set; }
    public string? IpAddress { get; set; }
    public short? Port { get; set; }
}
