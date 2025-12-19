// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Services.Interfaces;
using MetadataExtractor;
using MetadataExtractor.Formats.QuickTime;

namespace MediaPi.Core.Services;

public class VideoMetadataService(ILogger<VideoMetadataService> logger) : IVideoMetadataService
{
    private readonly ILogger<VideoMetadataService> _logger = logger;

    public async Task<VideoMetadata?> ExtractMetadataAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            _logger.LogWarning("Video file not found: {FilePath}", filePath);
            return null;
        }

        var res = new VideoMetadata
        {
            FileSizeBytes = 0,
            DurationSeconds = null
        };

        try
        {
            // Get file size
            var fileInfo = new FileInfo(filePath);
            res.FileSizeBytes = ConvertFileSizeToUInt(fileInfo.Length);

            // Extract metadata using MetadataExtractor
            await using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                res.DurationSeconds = await Task.Run(() => ExtractVideoMetadata(stream), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract metadata from video file: {FilePath}", filePath);           
        }
        return res;

    }

    private uint? ExtractVideoMetadata(Stream fileStream)
    {
        uint? res = null;
        
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(fileStream);
            
            foreach (var directory in directories)
            {
                double? duration = ExtractDuration(directory);
                if (duration is not null)
                {
                    res = ConvertDurationToUInt(duration);
                    break;
                }

                }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MetadataExtractor failed for file stream: {FilePath}", fileStream);
        }

        return res;
    }

    private static double? ExtractDuration(MetadataExtractor.Directory directory)
    {
        try
        {
            // Try QuickTime movie header for MP4/MOV files
            if (directory is QuickTimeMovieHeaderDirectory movieHeader &&
                movieHeader.TryGetInt32(QuickTimeMovieHeaderDirectory.TagDuration, out var qtDuration) &&
                movieHeader.TryGetInt32(QuickTimeMovieHeaderDirectory.TagTimeScale, out var qtTimeScale) &&
                qtTimeScale > 0)
            {
                return (double)qtDuration / qtTimeScale;
            }

            // Generic approach - look for duration-related tags
            foreach (var tag in directory.Tags)
            {
                var tagName = tag.Name?.ToLowerInvariant();
                if (tagName != null && (tagName.Contains("duration") || tagName.Contains("length")))
                {
                    var description = tag.Description;
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        // Try to parse duration from description
                        var duration = ParseDurationFromDescription(description);
                        if (duration.HasValue)
                        {
                            return duration.Value;
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            // Ignore individual extraction failures
        }

        return null;
    }

    private static double? ParseDurationFromDescription(string description)
    {
        try
        {
            // Try to parse common duration formats
            // Format: "HH:MM:SS" or "MM:SS"
            if (TimeSpan.TryParse(description, out var timeSpan))
            {
                return timeSpan.TotalSeconds;
            }

            // Format: "X seconds" or "X sec" or "X s"
            var secondsPatterns = new[] { " seconds", " sec", " s" };
            foreach (var pattern in secondsPatterns.Where(p => description.EndsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                var numberPart = description[..^pattern.Length].Trim();
                if (double.TryParse(numberPart, out var seconds))
                {
                    return seconds;
                }
            }

            // Format: numbers only (assume seconds)
            // Only consider it duration if it's a reasonable value (between 0.1 and 86400 seconds = 24 hours)
            if (double.TryParse(description.Trim(), out var numericValue) && numericValue >= 0.1 && numericValue <= 86400)
            {
                return numericValue;
            }
        }
        catch (Exception)
        {
            // Ignore parsing failures
        }

        return null;
    }

    private static uint? ConvertDurationToUInt(double? durationSeconds)
    {
        if (!durationSeconds.HasValue)
            return null;

        // Round to nearest second and ensure non-negative
        var roundedDuration = Math.Max(0, Math.Round(durationSeconds.Value));
        
        // Ensure it fits in uint range
        if (roundedDuration > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), 
                $"Duration {roundedDuration} seconds exceeds maximum supported duration of {uint.MaxValue} seconds (~136 years)");
        }

        return (uint)roundedDuration;
    }

    private static uint ConvertFileSizeToUInt(long fileSizeBytes)
    {
        // Handle negative sizes (shouldn't happen, but just in case)
        if (fileSizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fileSizeBytes), "File size cannot be negative");
        }

        if (fileSizeBytes == 0)
            return 0;

        // Cap at uint.MaxValue for files larger than ~4GB
        if (fileSizeBytes > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(fileSizeBytes), 
                $"File size {fileSizeBytes} bytes exceeds maximum supported size of {uint.MaxValue} bytes (4GB)");
        }
 
        return (uint)fileSizeBytes;
    }

}