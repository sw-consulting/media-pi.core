// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Globalization;
using System.Text.Json;

using MediaPi.Core.Models;
using MediaPi.Core.Settings;

namespace MediaPi.Core.RestModels;

public class VideoViewItem(Video video)
{
    public int Id { get; init; } = video.Id;
    public string Title { get; init; } = video.Title;
    public string Filename { get; init; } = video.Filename;
    public string OriginalFilename { get; init; } = video.OriginalFilename;
    public uint FileSizeBytes { get; init; } = video.FileSizeBytes;
    public uint? DurationSeconds { get; init; } = video.DurationSeconds;
    public int AccountId { get; init; } = video.AccountId ?? 0;
    
    // Formatted string properties
    public string FileSize { get; init; } = FormatFileSize(video.FileSizeBytes);
    public string Duration { get; init; } = FormatDuration(video.DurationSeconds);

    /// <summary>
    /// Formats file size in bytes to human-readable format with Russian units
    /// </summary>
    /// <param name="sizeBytes">File size in bytes</param>
    /// <returns>Formatted string like "1.24 Ãá", "1.71 Ìá", "240 Êá", "870 áàéò"</returns>
    private static string FormatFileSize(uint sizeBytes)
    {
        if (sizeBytes == 0)
            return "0 áàéò";

        const uint kilobyte = 1024;
        const uint megabyte = kilobyte * 1024;
        const ulong gigabyte = (ulong)megabyte * 1024;
        const ulong terabyte = gigabyte * 1024;

        // Convert to ulong to handle calculations for sizes near uint.MaxValue
        ulong size = sizeBytes;

        return size switch
        {
            >= terabyte => $"{(size / (double)terabyte).ToString("F2", CultureInfo.InvariantCulture)} Òá",
            >= gigabyte => $"{(size / (double)gigabyte).ToString("F2", CultureInfo.InvariantCulture)} Ãá",
            >= megabyte => $"{(size / (double)megabyte).ToString("F2", CultureInfo.InvariantCulture)} Ìá",
            >= kilobyte => $"{(size / (double)kilobyte).ToString("F0", CultureInfo.InvariantCulture)} Êá",
            _ => $"{size} áàéò"
        };
    }

    /// <summary>
    /// Formats duration in seconds to HH:mm:ss format or "íå èçâåñòíî" if null
    /// </summary>
    /// <param name="durationSeconds">Duration in seconds or null</param>
    /// <returns>Formatted string like "01:23:45" or "íå èçâåñòíî"</returns>
    private static string FormatDuration(uint? durationSeconds)
    {
        if (!durationSeconds.HasValue)
            return "íå èçâåñòíî";

        var totalSeconds = durationSeconds.Value;
        
        // Handle potential overflow for very large uint values
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;

        var hoursPart = hours < 100
            ? hours.ToString("D2", CultureInfo.InvariantCulture)
            : hours.ToString(CultureInfo.InvariantCulture);

        return $"{hoursPart}:{minutes:D2}:{seconds:D2}";
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}
