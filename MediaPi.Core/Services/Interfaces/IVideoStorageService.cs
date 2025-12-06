// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using Microsoft.AspNetCore.Http;

namespace MediaPi.Core.Services.Interfaces;

public interface IVideoStorageService
{
    Task<VideoSaveResult> SaveVideoAsync(IFormFile file, string title, CancellationToken ct = default);
    Task DeleteVideoAsync(string storedFilename, CancellationToken ct = default);
    string GetAbsolutePath(string storedFilename);
}

public class VideoSaveResult
{
    public required string Filename { get; set; }
    public required string OriginalFilename { get; init; }
    public required uint FileSizeBytes { get; init; }
    public uint? DurationSeconds { get; set; }
    public string? Format { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}
