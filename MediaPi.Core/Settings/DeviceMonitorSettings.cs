// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.Settings;

public class DeviceMonitorSettings
{
    public int OnlinePollingIntervalSeconds { get; set; } = 600;
    public int OfflinePollingIntervalSeconds { get; set; } = 60;
    public int FallbackIntervalSeconds { get; set; } = 60;
    public int MaxParallelProbes { get; set; } = 20;
    public int TimeoutSeconds { get; set; } = 5;
    public int JitterSeconds { get; set; } = 5;
}
