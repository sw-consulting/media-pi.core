// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json.Serialization;
namespace MediaPi.Core.RestModels.Device
{
    public class PlaylistSettingsDto
    {
        [JsonPropertyName("source")]
        public required string Source { get; init; }

        [JsonPropertyName("destination")]
        public required string Destination { get; init; }
    }
}
