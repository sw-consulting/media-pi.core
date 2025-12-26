// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.RestModels;

public class DeviceSyncManifestItem
{
    public int Id { get; init; }
    public string Filename { get; init; } = string.Empty;
    public uint FileSizeBytes { get; init; }
    public string Sha256 { get; init; } = string.Empty;
}
