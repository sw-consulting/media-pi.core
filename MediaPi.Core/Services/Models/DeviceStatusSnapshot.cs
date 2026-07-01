// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.RestModels.Device;

namespace MediaPi.Core.Services.Models;

public class DeviceStatusSnapshot
{
    public string IpAddress { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTimeOffset? LastChecked { get; set; }
    public DateTimeOffset ServerLastChecked { get; set; }
    public long ConnectLatencyMs { get; set; }
    public long TotalLatencyMs { get; set; }
    public string? SoftwareVersion { get; set; }
    public bool? PlaybackServiceStatus { get; set; }
    public bool? PlaylistUploadServiceStatus { get; set; }
    public bool? VideoUploadServiceStatus { get; set; }
    public PlaylistActivationDto? PlaylistActivation { get; set; }
}
