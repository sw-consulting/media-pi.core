// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json.Serialization;

namespace MediaPi.Core.RestModels.Device
{
    public class AudioSettingsDto
    {
        [JsonPropertyName("output")]
        public string Output { get; set; } = string.Empty;
    }
}
