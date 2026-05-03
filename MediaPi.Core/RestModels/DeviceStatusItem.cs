// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;
using MediaPi.Core.Services.Models;
using MediaPi.Core.Settings;

namespace MediaPi.Core.RestModels;

public class DeviceStatusItem
{
    public int DeviceId { get; set; }
    public bool IsOnline { get; set; }
    public DateTime LastChecked { get; set; }
    public long ConnectLatencyMs { get; set; }
    public long TotalLatencyMs { get; set; }
    public string? SoftwareVersion { get; set; }
    public bool? PlaybackServiceStatus { get; set; }
    public bool? PlaylistUploadServiceStatus { get; set; }
    public bool? VideoUploadServiceStatus { get; set; }

    // Parameterless constructor for JSON deserialization
    public DeviceStatusItem() { }

    public DeviceStatusItem(int deviceId, DeviceStatusSnapshot snapshot)
    {
        DeviceId = deviceId;
        IsOnline = snapshot.IsOnline;
        LastChecked = snapshot.LastChecked;
        ConnectLatencyMs = snapshot.ConnectLatencyMs;
        TotalLatencyMs = snapshot.TotalLatencyMs;
        SoftwareVersion = snapshot.SoftwareVersion;
        PlaybackServiceStatus = snapshot.PlaybackServiceStatus;
        PlaylistUploadServiceStatus = snapshot.PlaylistUploadServiceStatus;
        VideoUploadServiceStatus = snapshot.VideoUploadServiceStatus;
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}
