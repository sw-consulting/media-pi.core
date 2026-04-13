// Copyright (c) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

using MediaPi.Core.Services.Interfaces;
using MediaPi.Core.Settings;

namespace MediaPi.Core.Services;

public partial class ScreenshotStorageService : FileStorageService, IScreenshotStorageService
{
    protected override string DefaultTitleToken => "screenshot";

    public ScreenshotStorageService(IOptions<VideoStorageSettings> options)
        : base(options)
    {
    }

    public async Task<ScreenshotSaveResult> SaveScreenshotAsync(IFormFile file, string title, CancellationToken ct = default)
    {
        var fileResult = await SaveFileAsync(file, title, computeSha256: false, ct);

        return new ScreenshotSaveResult
        {
            Filename = fileResult.Filename,
            OriginalFilename = fileResult.OriginalFilename,
            FileSizeBytes = fileResult.FileSizeBytes,
            Sha256 = fileResult.Sha256,
            TimeCreated = ExtractTimeCreated(fileResult.OriginalFilename)
        };
    }

    public Task DeleteScreenshotAsync(string storedFilename, CancellationToken ct = default)
    {
        return DeleteFileAsync(storedFilename, ct);
    }

    private static DateTime ExtractTimeCreated(string originalFilename)
    {
        var basename = Path.GetFileName(originalFilename);
        var match = CamScreenshotPattern().Match(basename);
        if (!match.Success)
        {
            return DateTime.UtcNow;
        }

        var timestamp = match.Groups["timestamp"].Value;
        if (!DateTime.TryParseExact(timestamp, "yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return DateTime.UtcNow;
        }

        return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
    }

    [GeneratedRegex("^cam_(?<timestamp>\\d{4}-\\d{2}-\\d{2}_\\d{2}-\\d{2}-\\d{2})\\.jpg$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CamScreenshotPattern();
}
