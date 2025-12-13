// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.Services.Models;

public record DeviceProbeResult(
    bool IsOnline,
    long ConnectMs,
    long TotalMs,
    string? SoftwareVersion);