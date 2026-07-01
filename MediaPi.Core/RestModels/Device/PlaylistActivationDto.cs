// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json.Serialization;

namespace MediaPi.Core.RestModels.Device
{
    public class PlaylistActivationDto
    {
        [JsonPropertyName("state")]
        public string? State { get; init; }

        [JsonPropertyName("phase")]
        public string? Phase { get; init; }

        [JsonPropertyName("trigger")]
        public string? Trigger { get; init; }

        [JsonPropertyName("startedAt")]
        public DateTimeOffset? StartedAt { get; init; }

        [JsonPropertyName("finishedAt")]
        public DateTimeOffset? FinishedAt { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }
}
