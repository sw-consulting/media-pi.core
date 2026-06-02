// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.RestModels;

public class VideoBatchDeleteItem
{
    public List<int> Ids { get; set; } = [];
}

public class VideoBatchDeleteResult
{
    public int RequestedCount { get; init; }
    public List<int> DeletedIds { get; init; } = [];
    public List<VideoBatchDeleteFailure> Failures { get; init; } = [];
}

public class VideoBatchOperationFailure
{
    public required int Id { get; init; }
    public required string Reason { get; init; }
    public required string Message { get; init; }
}

public class VideoBatchDeleteFailure : VideoBatchOperationFailure;

public class VideoBatchCategoryUpdateItem
{
    public List<int> Ids { get; set; } = [];
    public int? CategoryId { get; set; }
    public bool ForcePlaylistCleanup { get; set; }
}

public class VideoBatchCategoryUpdateResult
{
    public int RequestedCount { get; init; }
    public List<int> UpdatedIds { get; init; } = [];
    public List<VideoBatchOperationFailure> Failures { get; init; } = [];
}
