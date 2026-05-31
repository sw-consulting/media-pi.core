// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.RestModels;

public class VideoUpdateItem
{
    public string? Title { get; set; }

    public List<int>? PlaylistIds { get; set; }

    public int? CategoryId { get; set; }

    public bool ForcePlaylistCleanup { get; set; }
}
