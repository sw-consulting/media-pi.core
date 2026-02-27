// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.Services.Interfaces;

public interface IVideoMetadataService
{
    /// <summary>
    /// Extracts metadata from a video file including file size, duration, and SHA256 hash
    /// </summary>
    /// <param name="filePath">Full path to the video file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Video metadata or null if extraction fails</returns>
    Task<VideoMetadata?> ExtractMetadataAsync(string filePath, CancellationToken cancellationToken = default);
}

public class VideoMetadata
{
    public required uint FileSizeBytes { get; set; }
    public uint? DurationSeconds { get; set; }
    public string? Sha256 { get; set; }
}