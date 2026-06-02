// Copyright (C) 2026 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.RestModels;

public class VideoPlaybackTokenViewItem
{
    public required string Token { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required string Url { get; init; }
}
