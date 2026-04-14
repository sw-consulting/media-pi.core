// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using Microsoft.AspNetCore.Http;

namespace MediaPi.Core.RestModels;

public class VideoUploadItem
{
    public required string Title { get; set; }

    public required int AccountId { get; set; }

    public required IFormFile File { get; set; }
}
