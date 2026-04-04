// Copyright (c) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using Microsoft.AspNetCore.Http;

namespace MediaPi.Core.Services.Interfaces;

public interface IFileStorageService
{
    Task<FileSaveResult> SaveFileAsync(IFormFile file, string title, bool computeSha256 = false, CancellationToken ct = default);
    Task DeleteFileAsync(string storedFilename, CancellationToken ct = default);
    string GetAbsolutePath(string storedFilename);
}

public class FileSaveResult
{
    public required string Filename { get; init; }
    public required string OriginalFilename { get; init; }
    public required uint FileSizeBytes { get; init; }
    public string? Sha256 { get; init; }
}
