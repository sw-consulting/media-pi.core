// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.RestModels;

public static class ConflictReasons
{
    public const string DuplicateOriginalFilename = "duplicateOriginalFilename";
    public const string DuplicateVideoDescription = "duplicateVideoDescription";
    public const string DuplicatePlaylistDescription = "duplicatePlaylistDescription";
    public const string DuplicatePlaylistFilename = "duplicatePlaylistFilename";
    public const string VideoStorageSaveFailed = "videoStorageSaveFailed";
    public const string VideoUploadCleanupFailed = "videoUploadCleanupFailed";
    public const string VideoUploadProcessingFailed = "videoUploadProcessingFailed";
    public const string VideoUploadTooLarge = "videoUploadTooLarge";
}
