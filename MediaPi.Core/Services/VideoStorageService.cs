// Copyright (c) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

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
        var fileResult = await SaveFileAsync(file, title, computeSha256: true, ct);
        var fullPath = GetAbsolutePath(fileResult.Filename);
        var metadata = await _metadataService.ExtractMetadataAsync(fullPath, ct, fileResult.Sha256);

        return new VideoSaveResult
        {
            Filename = fileResult.Filename,
            OriginalFilename = fileResult.OriginalFilename,
            FileSizeBytes = metadata?.FileSizeBytes ?? fileResult.FileSizeBytes,
            DurationSeconds = metadata?.DurationSeconds,
            Sha256 = fileResult.Sha256
        };
    }

    public Task DeleteVideoAsync(string storedFilename, CancellationToken ct = default)
    {
        return DeleteFileAsync(storedFilename, ct);
    }
}
