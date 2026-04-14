
// This file is a part of Media Pi backend

using Microsoft.AspNetCore.Http;

namespace MediaPi.Core.Services.Interfaces;

public interface IScreenshotStorageService : IFileStorageService
{
    Task<ScreenshotSaveResult> SaveScreenshotAsync(IFormFile file, string title, CancellationToken ct = default);
    Task DeleteScreenshotAsync(string storedFilename, CancellationToken ct = default);
}

public class ScreenshotSaveResult : FileSaveResult
{
    public required DateTime TimeCreated { get; init; }
}
