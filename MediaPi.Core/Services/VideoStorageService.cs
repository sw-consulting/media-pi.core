// Copyright (c) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.Security.Cryptography;
using Microsoft.Extensions.Options;

using MediaPi.Core.Services.Interfaces;
using MediaPi.Core.Settings;

namespace MediaPi.Core.Services;

public class VideoStorageService : FileStorageService, IVideoStorageService
{
    private readonly IVideoMetadataService _metadataService;

    protected override string DefaultTitleToken => "video";

    public VideoStorageService(IOptions<VideoStorageSettings> options, IVideoMetadataService metadataService)
        : base(options)
    {
        _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
    }

    public async Task<VideoSaveResult> SaveVideoAsync(IFormFile file, string title, CancellationToken ct = default)
    {
        var fileResult = await SaveFileAsync(file, title, ct);
        var fullPath = GetAbsolutePath(fileResult.Filename);
        var sha256Hash = await CalculateSha256Async(fullPath, ct);
        var metadata = await _metadataService.ExtractMetadataAsync(fullPath, ct, sha256Hash);

        return new VideoSaveResult
        {
            Filename = fileResult.Filename,
            OriginalFilename = fileResult.OriginalFilename,
            FileSizeBytes = metadata?.FileSizeBytes ?? fileResult.FileSizeBytes,
            DurationSeconds = metadata?.DurationSeconds,
            Sha256 = sha256Hash
        };
    }

    public Task DeleteVideoAsync(string storedFilename, CancellationToken ct = default)
    {
        return DeleteFileAsync(storedFilename, ct);
    }

    private static async Task<string> CalculateSha256Async(string filePath, CancellationToken ct)
    {
        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(fs, ct);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}
