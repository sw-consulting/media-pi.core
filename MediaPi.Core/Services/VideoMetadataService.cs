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

        try
        {
            // Get file size
            var fileInfo = new FileInfo(filePath);
            var fileSizeBytes = ConvertFileSizeToUInt(fileInfo.Length);

            // Extract metadata using MetadataExtractor
            var metadata = await Task.Run(() => ExtractVideoMetadata(filePath), cancellationToken);

            return new VideoMetadata
            {
                FileSizeBytes = fileSizeBytes,
                DurationSeconds = ConvertDurationToUInt(metadata.DurationSeconds)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract metadata from video file: {FilePath}", filePath);
            
            // Return basic metadata with just file size if extraction fails
            try
            {
                var fileInfo = new FileInfo(filePath);
                return new VideoMetadata
                {
                    FileSizeBytes = ConvertFileSizeToUInt(fileInfo.Length),
                    DurationSeconds = null
                };
            }
            catch
            {
                return null;
            }
        }
    }

    private VideoMetadata ExtractVideoMetadata(string filePath)
    {
        var result = new InternalVideoMetadata();
        
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);
            
            foreach (var directory in directories)
            {
                // Extract duration from various directory types
                if (result.DurationSeconds == null)
                {
                    result.DurationSeconds = ExtractDuration(directory);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MetadataExtractor failed for file: {FilePath}", filePath);
        }

        return new VideoMetadata
        {
            FileSizeBytes = ConvertFileSizeToUInt(0), // Default to 0 if extraction fails
            DurationSeconds = ConvertDurationToUInt(result.DurationSeconds)
        };
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

    private class InternalVideoMetadata
    {
        public double? DurationSeconds { get; set; }
    }
}