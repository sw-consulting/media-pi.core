// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using Microsoft.AspNetCore.Http;

namespace MediaPi.Core.Services.Interfaces;

public interface IVideoStorageService : IFileStorageService
{
    Task<VideoSaveResult> SaveVideoAsync(IFormFile file, string title, CancellationToken ct = default);
    Task DeleteVideoAsync(string storedFilename, CancellationToken ct = default);
}

public class VideoSaveResult : FileSaveResult
{
    public uint? DurationSeconds { get; init; }
}
