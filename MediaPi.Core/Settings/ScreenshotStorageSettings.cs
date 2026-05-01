// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.Settings;

public class ScreenshotStorageSettings
{
    public string RootPath { get; set; } = "/var/lib/storage/screenshots";
    public int MaxFilesPerDirectory { get; set; } = 1000;
}
