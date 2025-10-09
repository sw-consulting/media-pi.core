// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.RestModels;

public class VideoUploadItem
{
    public required string Title { get; set; } = string.Empty;

    public required int AccountId { get; set; }

    public required IFormFile File { get; set; }
}
