// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Models;

namespace MediaPi.Core.RestModels;

public class ScreenshotViewItem(Screenshot screenshot)
{
    public int Id { get; init; } = screenshot.Id;
    public string Filename { get; init; } = screenshot.Filename;
    public string OriginalFilename { get; init; } = screenshot.OriginalFilename;
    public uint FileSizeBytes { get; init; } = screenshot.FileSizeBytes;
    public DateTime TimeCreated { get; init; } = screenshot.TimeCreated;
    public int DeviceId { get; init; } = screenshot.DeviceId;
}
