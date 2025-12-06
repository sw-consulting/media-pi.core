// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;

using MediaPi.Core.Models;
using MediaPi.Core.Settings;

namespace MediaPi.Core.RestModels;

public class PlaylistViewItem(Playlist playlist)
{
    public int Id { get; set; } = playlist.Id;
    public string Title { get; set; } = playlist.Title;
    public string Filename { get; set; } = playlist.Filename;
    public int AccountId { get; set; } = playlist.AccountId;
    public IEnumerable<int> VideoIds { get; set; } = [.. playlist.VideoPlaylists.Select(vp => vp.VideoId)];
    
    // New properties for enhanced playlist support
    public IEnumerable<PlaylistItemDto> Items { get; set; } = playlist.VideoPlaylists
        .OrderBy(vp => vp.Position)
        .Select(vp => new PlaylistItemDto { VideoId = vp.VideoId, Position = vp.Position });
    
    public PlaylistStatsDto Stats { get; set; } = CalculateStats(playlist);
    
    private static PlaylistStatsDto CalculateStats(Playlist playlist)
    {
        if (playlist.VideoPlaylists == null || !playlist.VideoPlaylists.Any())
        {
            return new PlaylistStatsDto { TotalFileSizeBytes = 0, TotalDurationSeconds = 0, VideoCount = 0 };
        }

        // Get unique videos to avoid counting duplicates in size/duration calculations
        var uniqueVideos = playlist.VideoPlaylists
            .Where(vp => vp.Video != null)
            .GroupBy(vp => vp.VideoId)
            .Select(g => g.First().Video)
            .ToList();

        var totalSize = uniqueVideos.Sum(v => (long)v.FileSizeBytes);
        var totalDuration = uniqueVideos.All(v => v.DurationSeconds.HasValue) 
            ? (uint?)uniqueVideos.Sum(v => (long)v.DurationSeconds!.Value)
            : null;

        return new PlaylistStatsDto
        {
            TotalFileSizeBytes = totalSize > uint.MaxValue ? uint.MaxValue : (uint)totalSize,
            TotalDurationSeconds = totalDuration,
            VideoCount = playlist.VideoPlaylists.Count // Include duplicates in count
        };
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}
