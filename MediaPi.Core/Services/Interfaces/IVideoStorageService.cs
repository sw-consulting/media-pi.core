// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using Microsoft.AspNetCore.Http;

namespace MediaPi.Core.Services.Interfaces;

public interface IVideoStorageService
{
    Task<string> SaveVideoAsync(IFormFile file, string title, CancellationToken ct = default);
    Task DeleteVideoAsync(string storedFilename, CancellationToken ct = default);
    string GetAbsolutePath(string storedFilename);
}
