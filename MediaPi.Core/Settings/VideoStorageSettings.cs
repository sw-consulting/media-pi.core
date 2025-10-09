// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.Settings;

public class VideoStorageSettings
{
    public string RootPath { get; set; } = "/var/lib/storage";
    public int MaxFilesPerDirectory { get; set; } = 1000;
}
