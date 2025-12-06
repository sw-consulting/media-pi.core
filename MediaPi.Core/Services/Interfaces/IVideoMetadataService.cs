// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.Services.Interfaces;

public interface IVideoMetadataService
{
    /// <summary>
    /// Extracts metadata from a video file including duration and file size
    /// </summary>
    /// <param name="filePath">Full path to the video file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Video metadata or null if extraction fails</returns>
    Task<VideoMetadata?> ExtractMetadataAsync(string filePath, CancellationToken cancellationToken = default);
}

public class VideoMetadata
{
    public required uint FileSizeBytes { get; init; }
    public uint? DurationSeconds { get; set; }
    public string? Format { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? VideoCodec { get; set; }
    public string? AudioCodec { get; set; }
}