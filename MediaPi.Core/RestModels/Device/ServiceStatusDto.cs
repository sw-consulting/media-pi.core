// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json.Serialization;

namespace MediaPi.Core.RestModels.Device
{
    public class ServiceStatusDto
    {
        [JsonPropertyName("playbackServiceStatus")]
        public bool PlaybackServiceStatus { get; init; }

        [JsonPropertyName("playlistUploadServiceStatus")]
        public bool PlaylistUploadServiceStatus { get; init; }

        [JsonPropertyName("yaDiskMountStatus")]
        public bool YaDiskMountStatus { get; init; }
    }
}
