// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.Services.Models;

public class DeviceStatusSnapshot
{
    public string IpAddress { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime LastChecked { get; set; }
    public long ConnectLatencyMs { get; set; }
    public long TotalLatencyMs { get; set; }
}
