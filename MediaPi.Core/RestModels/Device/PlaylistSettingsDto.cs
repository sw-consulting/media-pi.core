// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.RestModels.Device
{
    public class PlaylistSettingsDto
    {
        public required string Source { get; init; }
        public required string Destination { get; init; }
    }
}
