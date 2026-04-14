// Copyright (C) 2025-2026 sw.consulting
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
    
    public IEnumerable<PlaylistItemDto> Items { get; set; }
    
    /// <summary>
    /// Total file size of unique videos in bytes. Can exceed uint.MaxValue when playlists contain many large videos.
    /// Uses ulong to support up to 18.4 exabytes total size.
    /// </summary>
    public ulong TotalFileSizeBytes { get; set; }
    public uint? TotalDurationSeconds { get; set; }
    public int VideoCount { get; set; }

    public PlaylistViewItem(Playlist playlist)
    {
        Id = playlist.Id;
        Title = playlist.Title;
        Filename = playlist.Filename;
        AccountId = playlist.AccountId;
        Items = playlist.VideosPlaylist
            .OrderBy(vp => vp.Position)
            .Select(vp => new PlaylistItemDto { VideoId = vp.VideoId, Position = vp.Position });

        // Calculate stats once
        var stats = CalculateStats(playlist);
        TotalFileSizeBytes = stats.TotalFileSizeBytes;
        TotalDurationSeconds = stats.TotalDurationSeconds;
        VideoCount = stats.VideoCount;
    }
    
    private static (ulong TotalFileSizeBytes, uint? TotalDurationSeconds, int VideoCount) CalculateStats(Playlist playlist)
    {
        if (playlist.VideosPlaylist == null || !playlist.VideosPlaylist.Any())
        {
            return (0, null, 0);
        }

        var videoCount = playlist.VideosPlaylist.Count; // Include duplicates in count

        // For file size: Get unique videos to avoid counting duplicates
        var uniqueVideos = playlist.VideosPlaylist
            .Where(vp => vp.Video != null)
            .GroupBy(vp => vp.VideoId)
            .Select(g => g.First().Video)
            .ToList();

        // Use ulong to handle large total sizes (sum of many 4GB files)
        var totalSize = uniqueVideos.Aggregate(0UL, (acc, v) => acc + (ulong)v.FileSizeBytes);

        // For duration: Count all instances including duplicates
        var videosWithDuration = playlist.VideosPlaylist
            .Where(vp => vp.Video != null && vp.Video.DurationSeconds.HasValue)
            .Select(vp => vp.Video)
            .ToList();

        uint? totalDuration = null;
        if (videosWithDuration.Count == playlist.VideosPlaylist.Count)
        {
            // All videos have duration data
            var durationSum = videosWithDuration.Sum(v => (long)v.DurationSeconds!.Value);
            totalDuration = durationSum > uint.MaxValue ? uint.MaxValue : (uint)durationSum;
        }

        return (totalSize, totalDuration, videoCount);
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}
