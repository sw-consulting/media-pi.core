// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Diagnostics;
using System.Globalization;
using MediaPi.Core.Services.Interfaces;

namespace MediaPi.Core.Services;

public class VideoMetadataService(ILogger<VideoMetadataService> logger) : IVideoMetadataService
{
    private readonly ILogger<VideoMetadataService> _logger = logger;
    private const string MediaInfoCommand = "mediainfo";

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

            // Extract metadata using MediaInfo command line tool
            res.DurationSeconds = await ExtractVideoMetadataAsync(filePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract metadata from video file: {FilePath}", filePath);           
        }
        return res;
    }

    private async Task<uint?> ExtractVideoMetadataAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            // Use mediainfo command to get duration in seconds
            var arguments = $"--Output=\"General;%Duration/String3%\" \"{filePath}\"";
            
            var processInfo = new ProcessStartInfo
            {
                FileName = MediaInfoCommand,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processInfo };
            
            process.Start();
            
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            
            await process.WaitForExitAsync(cancellationToken);
            
            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                _logger.LogDebug("MediaInfo process exited with code {ExitCode}. Error: {Error}", 
                    process.ExitCode, error);
                return null;
            }

            return ParseDurationOutput(output?.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MediaInfo extraction failed for file: {FilePath}", filePath);
            return null;
        }
    }

    private uint? ParseDurationOutput(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        try
        {
            // MediaInfo returns duration in format "HH:MM:SS.mmm" or "MM:SS.mmm"
            if (TimeSpan.TryParse(output, CultureInfo.InvariantCulture, out var timeSpan))
            {
                return ConvertDurationToUInt(timeSpan.TotalSeconds);
            }

            // Fallback: try to parse as decimal seconds
            if (double.TryParse(output, CultureInfo.InvariantCulture, out var seconds))
            {
                return ConvertDurationToUInt(seconds);
            }

            _logger.LogDebug("Unable to parse duration output: {Output}", output);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse MediaInfo duration output: {Output}", output);
            return null;
        }
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