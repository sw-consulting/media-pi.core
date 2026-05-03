// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.RestModels.Device;

public class HealthInfo
{
    public string? Status { get; init; }
    public double? Uptime { get; init; }
    public string? Version { get; init; }
    public ServiceStatusDto? ServiceStatus { get; init; }
}
