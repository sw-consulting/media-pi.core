// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;

using MediaPi.Core.Models;
using MediaPi.Core.Settings;

namespace MediaPi.Core.RestModels;

public class PlaylistViewItem
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Filename { get; set; }
    public int AccountId { get; set; }
    public IEnumerable<int> VideoIds { get; set; }
    
    public IEnumerable<PlaylistItemDto> Items { get; set; }
    
    // Playlist statistics calculated once in constructor
    public uint TotalFileSizeBytes { get; set; }
    public uint? TotalDurationSeconds { get; set; }
    public int VideoCount { get; set; }

    public PlaylistViewItem(Playlist playlist)
    {
        Id = playlist.Id;
        Title = playlist.Title;
        Filename = playlist.Filename;
        AccountId = playlist.AccountId;
        VideoIds = [.. playlist.VideoPlaylists.Select(vp => vp.VideoId)];
        Items = playlist.VideoPlaylists
            .OrderBy(vp => vp.Position)
            .Select(vp => new PlaylistItemDto { VideoId = vp.VideoId, Position = vp.Position });

        // Calculate stats once
        var stats = CalculateStats(playlist);
        TotalFileSizeBytes = stats.TotalFileSizeBytes;
        TotalDurationSeconds = stats.TotalDurationSeconds;
        VideoCount = stats.VideoCount;
    }
    
    private static (uint TotalFileSizeBytes, uint? TotalDurationSeconds, int VideoCount) CalculateStats(Playlist playlist)
    {
        if (playlist.VideoPlaylists == null || !playlist.VideoPlaylists.Any())
        {
            return (0, 0, 0);
        }

        var videoCount = playlist.VideoPlaylists.Count; // Include duplicates in count

        // For file size: Get unique videos to avoid counting duplicates
        var uniqueVideos = playlist.VideoPlaylists
            .Where(vp => vp.Video != null)
            .GroupBy(vp => vp.VideoId)
            .Select(g => g.First().Video)
            .ToList();

        var totalSize = uniqueVideos.Sum(v => (long)v.FileSizeBytes);
        var fileSizeBytes = totalSize > uint.MaxValue ? uint.MaxValue : (uint)totalSize;

        // For duration: Count all instances including duplicates
        var videosWithDuration = playlist.VideoPlaylists
            .Where(vp => vp.Video != null && vp.Video.DurationSeconds.HasValue)
            .Select(vp => vp.Video)
            .ToList();

        uint? totalDuration = null;
        if (videosWithDuration.Count == playlist.VideoPlaylists.Count)
        {
            // All videos have duration data
            var durationSum = videosWithDuration.Sum(v => (long)v.DurationSeconds!.Value);
            totalDuration = durationSum > uint.MaxValue ? uint.MaxValue : (uint)durationSum;
        }

        return (fileSizeBytes, totalDuration, videoCount);
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}
