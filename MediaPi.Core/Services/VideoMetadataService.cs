// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Services.Interfaces;
using MetadataExtractor;
using MetadataExtractor.Formats.QuickTime;
using Microsoft.Extensions.Logging;

namespace MediaPi.Core.Services;

public class VideoMetadataService : IVideoMetadataService
{
    private readonly ILogger<VideoMetadataService> _logger;

    public VideoMetadataService(ILogger<VideoMetadataService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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
                DurationSeconds = ConvertDurationToUInt(metadata.DurationSeconds),
                Format = metadata.Format,
                Width = metadata.Width,
                Height = metadata.Height,
                VideoCodec = metadata.VideoCodec,
                AudioCodec = metadata.AudioCodec
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

                // Extract video dimensions and format information
                ExtractVideoInfo(directory, result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MetadataExtractor failed for file: {FilePath}", filePath);
        }

        return new VideoMetadata
        {
            FileSizeBytes = ConvertFileSizeToUInt(0), // Default to 0 if extraction fails
            DurationSeconds = ConvertDurationToUInt(result.DurationSeconds),
            Format = result.Format,
            Width = result.Width,
            Height = result.Height,
            VideoCodec = result.VideoCodec,
            AudioCodec = result.AudioCodec
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

    private static void ExtractVideoInfo(MetadataExtractor.Directory directory, InternalVideoMetadata result)
    {
        try
        {
            // Extract video dimensions
            if (result.Width == null || result.Height == null)
            {
                ExtractDimensions(directory, result);
            }

            // Extract format and codec information
            ExtractFormatInfo(directory, result);
        }
        catch (Exception)
        {
            // Ignore individual extraction failures
        }
    }

    private static void ExtractDimensions(MetadataExtractor.Directory directory, InternalVideoMetadata result)
    {
        // Try to get width and height from various directory types
        var widthTags = new[] { "Image Width", "Width", "Video Width", "Frame Width" };
        var heightTags = new[] { "Image Height", "Height", "Video Height", "Frame Height" };

        foreach (var tag in directory.Tags)
        {
            var tagName = tag.Name;
            if (tagName != null)
            {
                // Check for width and height tags in a single block
                if ((result.Width == null && widthTags.Any(wt => tagName.Contains(wt, StringComparison.OrdinalIgnoreCase))) ||
                    (result.Height == null && heightTags.Any(ht => tagName.Contains(ht, StringComparison.OrdinalIgnoreCase))))
                {
                    if (result.Width == null && widthTags.Any(wt => tagName.Contains(wt, StringComparison.OrdinalIgnoreCase)) &&
                        directory.TryGetInt32(tag.Type, out var width) && width > 0)
                    {
                        result.Width = width;
                    }
                    if (result.Height == null && heightTags.Any(ht => tagName.Contains(ht, StringComparison.OrdinalIgnoreCase)) &&
                        directory.TryGetInt32(tag.Type, out var height) && height > 0)
                    {
                        result.Height = height;
                    }
                }
            }
        }
    }

    private static void ExtractFormatInfo(MetadataExtractor.Directory directory, InternalVideoMetadata result)
    {
        foreach (var tag in directory.Tags)
        {
            var tagName = tag.Name?.ToLowerInvariant();
            var description = tag.Description;

            if (string.IsNullOrWhiteSpace(tagName) || string.IsNullOrWhiteSpace(description))
                continue;

            // Extract format information
            if (result.Format == null && (tagName.Contains("format") || tagName.Contains("container")))
            {
                result.Format = description;
            }

            // Extract codec information
            if (result.VideoCodec == null && tagName.Contains("codec") && tagName.Contains("video"))
            {
                result.VideoCodec = description;
            }

            if (result.AudioCodec == null && tagName.Contains("codec") && tagName.Contains("audio"))
            {
                result.AudioCodec = description;
            }

            // Extract basic file type from directory name
            if (result.Format == null)
            {
                var directoryName = directory.GetType().Name?.ToLowerInvariant();
                if (directoryName != null)
                {
                    if (directoryName.Contains("quicktime") || directoryName.Contains("mp4"))
                    {
                        result.Format = "MP4/QuickTime";
                    }
                    else if (directoryName.Contains("avi"))
                    {
                        result.Format = "AVI";
                    }
                    else if (directoryName.Contains("mkv"))
                    {
                        result.Format = "Matroska";
                    }
                }
            }
        }
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
            foreach (var pattern in secondsPatterns)
            {
                if (description.EndsWith(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    var numberPart = description.Substring(0, description.Length - pattern.Length).Trim();
                    if (double.TryParse(numberPart, out var seconds))
                    {
                        return seconds;
                    }
                }
            }

            // Format: numbers only (assume seconds)
            if (double.TryParse(description.Trim(), out var numericValue))
            {
                // Only consider it duration if it's a reasonable value (between 0.1 and 86400 seconds = 24 hours)
                if (numericValue >= 0.1 && numericValue <= 86400)
                {
                    return numericValue;
                }
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
            return uint.MaxValue;

        return (uint)roundedDuration;
    }

    private static uint ConvertFileSizeToUInt(long fileSizeBytes)
    {
        // Handle negative sizes (shouldn't happen, but just in case)
        if (fileSizeBytes < 0)
            return 0;

        // Cap at uint.MaxValue for files larger than ~4GB
        if (fileSizeBytes > uint.MaxValue)
            return uint.MaxValue;

        return (uint)fileSizeBytes;
    }

    private class InternalVideoMetadata
    {
        public double? DurationSeconds { get; set; }
        public string? Format { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string? VideoCodec { get; set; }
        public string? AudioCodec { get; set; }
    }
}