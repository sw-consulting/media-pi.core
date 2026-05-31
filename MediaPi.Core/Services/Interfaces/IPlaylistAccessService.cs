// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.RestModels;

namespace MediaPi.Core.Services.Interfaces;

public interface IPlaylistAccessService
{
    Task<IReadOnlySet<int>> GetAccessibleVideoIdsForAccountAsync(int accountId, IEnumerable<int> videoIds, CancellationToken ct = default);
    Task<IReadOnlyList<int>> GetInaccessibleVideoIdsForAccountAsync(int accountId, IEnumerable<int> videoIds, CancellationToken ct = default);
    Task<bool> AccountCanAccessVideoAsync(int accountId, int videoId, CancellationToken ct = default);
    Task<PlaylistAccessImpact> BuildCurrentInvalidPlaylistImpactAsync(CancellationToken ct = default);
    Task<PlaylistAccessImpact> BuildCategoryFreeChangeImpactAsync(int categoryId, bool proposedFree, CancellationToken ct = default);
    Task<PlaylistAccessImpact> BuildVideoCategoryChangeImpactAsync(IEnumerable<int> videoIds, int? proposedCategoryId, CancellationToken ct = default);
    Task<PlaylistAccessImpact> BuildSubscriptionChangeImpactAsync(int accountId, int categoryId, DateTime proposedStartUtc, DateTime proposedEndUtc, CancellationToken ct = default);
    Task<PlaylistCleanupResult> RemovePlaylistItemsAsync(IEnumerable<int> videoPlaylistIds, CancellationToken ct = default);
    Task<PlaylistCleanupResult> RemoveCurrentInvalidPlaylistItemsAsync(CancellationToken ct = default);
}
