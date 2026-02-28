// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using MediaPi.Core.Services.Interfaces;

namespace MediaPi.Core.Services;

public class VideoMetadataService(ILogger<VideoMetadataService> logger) : IVideoMetadataService
{
    private readonly ILogger<VideoMetadataService> _logger = logger;
    private const string MediaInfoCommand = "mediainfo";
    private const int MediaInfoTimeoutSeconds = 30;

    public async Task<VideoMetadata?> ExtractMetadataAsync(string filePath, CancellationToken cancellationToken = default, string? precomputedSha256 = null)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            _logger.LogWarning("Video file not found: {FilePath}", filePath);
            return null;
        }

        var res = new VideoMetadata
        {
            FileSizeBytes = 0,
            DurationSeconds = null,
            Sha256 = null
        };

        try
        {
            // Get file size
            var fileInfo = new FileInfo(filePath);
            res.FileSizeBytes = ConvertFileSizeToUInt(fileInfo.Length);

            // Extract metadata using MediaInfo command line tool
            res.DurationSeconds = await ExtractVideoMetadataAsync(filePath, cancellationToken);

            // Calculate SHA256 hash (or use precomputed value if provided)
            res.Sha256 = precomputedSha256 ?? await CalculateSha256Async(filePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract metadata from video file: {FilePath}", filePath);           
        }
        return res;
    }

    /// <summary>
    /// Calculates the SHA256 hash of a file.
    /// </summary>
    private static async Task<string?> CalculateSha256Async(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
            using (var sha256 = SHA256.Create())
            {
                // Read file in chunks to handle large files efficiently
                var buffer = new byte[81920]; // 80KB buffer
                int bytesRead;
                while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
                }
                sha256.TransformFinalBlock(buffer, 0, 0);

                // Convert hash to lowercase hex string
                return Convert.ToHexString(sha256.Hash ?? Array.Empty<byte>()).ToLowerInvariant();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException($"Failed to calculate SHA256 for file: {filePath}", ex);
        }
    }

    private async Task<uint?> ExtractVideoMetadataAsync(string filePath, CancellationToken cancellationToken)
    {
        Process? process = null;
        try
        {
            // Use mediainfo command to get duration in seconds
            // Use ArgumentList for proper escaping to prevent command injection
            var processInfo = new ProcessStartInfo
            {
                FileName = MediaInfoCommand,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            processInfo.ArgumentList.Add("--Output=General;%Duration/String3%");
            processInfo.ArgumentList.Add(filePath);

            process = new Process { StartInfo = processInfo };
            
            process.Start();
            
            // Add timeout to prevent hung processes
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(MediaInfoTimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            
            var outputTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(linkedCts.Token);
            
            await process.WaitForExitAsync(linkedCts.Token);
            
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
        catch (OperationCanceledException)
        {
            // Ensure the process is killed when cancellation is requested
            if (process != null)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        _logger.LogDebug("MediaInfo process killed due to cancellation for file: {FilePath}", filePath);
                    }
                }
                catch (Exception killEx)
                {
                    _logger.LogDebug(killEx, "Failed to kill MediaInfo process for file: {FilePath}", filePath);
                }
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MediaInfo extraction failed for file: {FilePath}", filePath);
            return null;
        }
        finally
        {
            process?.Dispose();
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