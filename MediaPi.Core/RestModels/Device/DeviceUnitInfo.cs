// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;

namespace MediaPi.Core.RestModels.Device;

public class DeviceUnitInfo
{
    public string? Unit { get; init; }
    public JsonElement Active { get; init; }
    public JsonElement Sub { get; init; }
    public string? Error { get; init; }
}
