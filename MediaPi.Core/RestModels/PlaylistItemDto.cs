// Copyright (C) 2025-2026 sw.consulting
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
