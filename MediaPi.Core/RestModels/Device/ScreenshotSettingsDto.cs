// Copyright (C) 2026 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json.Serialization;

namespace MediaPi.Core.RestModels.Device
{
    public class ScreenshotSettingsDto
    {
        [JsonPropertyName("intervalMinutes")]
        public int IntervalMinutes { get; set; }
    }
}
