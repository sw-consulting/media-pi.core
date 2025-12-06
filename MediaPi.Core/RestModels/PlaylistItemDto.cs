// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.RestModels;

/// <summary>
/// Represents a playlist item with position for ordered playlists
/// </summary>
public class PlaylistItemDto
{
    public int VideoId { get; set; }
    public int Position { get; set; }
}

/// <summary>
/// Represents playlist statistics
/// </summary>
public class PlaylistStatsDto
{
    public uint TotalFileSizeBytes { get; set; }
    public uint? TotalDurationSeconds { get; set; }
    public int VideoCount { get; set; }
}