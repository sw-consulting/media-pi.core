// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json.Serialization;

namespace MediaPi.Core.RestModels.Device;

public class ConfigurationSettingsDto
{
    [JsonPropertyName("playlist")]
    public PlaylistSettingsDto Playlist { get; set; } = new()
    {
        Source = string.Empty,
        Destination = string.Empty
    };

    [JsonPropertyName("schedule")]
    public ScheduleSettingsDto Schedule { get; set; } = new();

    [JsonPropertyName("audio")]
    public AudioSettingsDto Audio { get; set; } = new()
    {
        Output = string.Empty
    };
}
