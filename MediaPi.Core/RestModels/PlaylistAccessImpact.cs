// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;
using System.Text.Json.Serialization;
using MediaPi.Core.Settings;

namespace MediaPi.Core.RestModels;

public class PlaylistAccessImpact
{
    public int AffectedPlaylistCount { get; init; }
    public int AffectedItemCount { get; init; }
    public int AffectedVideoCount { get; init; }
    public List<AffectedPlaylistItem> AffectedPlaylists { get; init; } = [];

    [JsonIgnore]
    public List<int> VideoPlaylistIds { get; init; } = [];

    public bool HasImpact => AffectedItemCount > 0;

    public override string ToString() => JsonSerializer.Serialize(this, JOptions.DefaultOptions);
}

public class AffectedPlaylistItem
{
    public int PlaylistId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Filename { get; init; } = string.Empty;
    public int AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public int RemovedItemCount { get; init; }
    public int AffectedVideoCount { get; init; }
}

public class PlaylistCleanupResult
{
    public int RemovedItemCount { get; init; }
    public int AffectedPlaylistCount { get; init; }
    public int AffectedVideoCount { get; init; }
}
