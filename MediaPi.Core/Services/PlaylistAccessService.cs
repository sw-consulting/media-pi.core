// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Data;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaPi.Core.Services;

public class PlaylistAccessService(AppDbContext db, ISubscriptionTimeService timeService) : IPlaylistAccessService
{
    private readonly AppDbContext _db = db;
    private readonly ISubscriptionTimeService _timeService = timeService;

    public async Task<IReadOnlySet<int>> GetAccessibleVideoIdsForAccountAsync(
        int accountId,
        IEnumerable<int> videoIds,
        CancellationToken ct = default)
    {
        var ids = videoIds.Distinct().ToList();
        if (ids.Count == 0) return new HashSet<int>();

        var videos = await _db.Videos
            .AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .Select(v => new VideoAccessRecord(
                v.Id,
                v.AccountId,
                v.CategoryId,
                v.Category != null && v.Category.Free))
            .ToListAsync(ct);

        var categoryIds = videos
            .Where(v => v.AccountId == null && v.CategoryId.HasValue && !v.CategoryFree)
            .Select(v => v.CategoryId!.Value)
            .Distinct()
            .ToList();
        var activeSubscriptions = await GetActiveSubscriptionKeysAsync([accountId], categoryIds, ct);

        return videos
            .Where(v => IsVideoAccessible(v, accountId, activeSubscriptions))
            .Select(v => v.VideoId)
            .ToHashSet();
    }

    public async Task<IReadOnlyList<int>> GetInaccessibleVideoIdsForAccountAsync(
        int accountId,
        IEnumerable<int> videoIds,
        CancellationToken ct = default)
    {
        var ids = videoIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        var accessible = await GetAccessibleVideoIdsForAccountAsync(accountId, ids, ct);
        return ids.Where(id => !accessible.Contains(id)).ToList();
    }

    public async Task<bool> AccountCanAccessVideoAsync(int accountId, int videoId, CancellationToken ct = default)
    {
        var accessible = await GetAccessibleVideoIdsForAccountAsync(accountId, [videoId], ct);
        return accessible.Contains(videoId);
    }

    public async Task<PlaylistAccessImpact> BuildCurrentInvalidPlaylistImpactAsync(CancellationToken ct = default)
    {
        var entries = await LoadPlaylistEntriesAsync(null, ct);
        return await BuildImpactAsync(entries, new AccessOverrides(), ct);
    }

    public async Task<PlaylistAccessImpact> BuildCategoryFreeChangeImpactAsync(
        int categoryId,
        bool proposedFree,
        CancellationToken ct = default)
    {
        var entries = await LoadPlaylistEntriesAsync(entry => entry.CategoryId == categoryId, ct);
        return await BuildImpactAsync(entries, new AccessOverrides
        {
            CategoryFreeOverrides = new Dictionary<int, bool> { [categoryId] = proposedFree }
        }, ct);
    }

    public async Task<PlaylistAccessImpact> BuildVideoCategoryChangeImpactAsync(
        IEnumerable<int> videoIds,
        int? proposedCategoryId,
        CancellationToken ct = default)
    {
        var ids = videoIds.Distinct().ToList();
        if (ids.Count == 0) return EmptyImpact();

        var entries = await LoadPlaylistEntriesAsync(entry => ids.Contains(entry.VideoId), ct);
        var categoryFreeOverrides = new Dictionary<int, bool>();
        if (proposedCategoryId.HasValue)
        {
            var proposedCategory = await _db.Categories
                .AsNoTracking()
                .Where(c => c.Id == proposedCategoryId.Value)
                .Select(c => new { c.Id, c.Free })
                .SingleAsync(ct);
            categoryFreeOverrides[proposedCategory.Id] = proposedCategory.Free;
        }

        return await BuildImpactAsync(entries, new AccessOverrides
        {
            VideoCategoryOverrides = ids.ToDictionary(id => id, _ => proposedCategoryId),
            CategoryFreeOverrides = categoryFreeOverrides
        }, ct);
    }

    public async Task<PlaylistAccessImpact> BuildSubscriptionChangeImpactAsync(
        int accountId,
        int categoryId,
        DateTime proposedStartUtc,
        DateTime proposedEndUtc,
        CancellationToken ct = default)
    {
        var entries = await LoadPlaylistEntriesAsync(
            entry => entry.PlaylistAccountId == accountId && entry.CategoryId == categoryId,
            ct);

        return await BuildImpactAsync(entries, new AccessOverrides
        {
            SubscriptionActiveOverrides = new Dictionary<(int AccountId, int CategoryId), bool>
            {
                [(accountId, categoryId)] = _timeService.IsActive(proposedStartUtc, proposedEndUtc)
            }
        }, ct);
    }

    public async Task<PlaylistCleanupResult> RemoveCurrentInvalidPlaylistItemsAsync(CancellationToken ct = default)
    {
        var impact = await BuildCurrentInvalidPlaylistImpactAsync(ct);
        return await RemovePlaylistItemsAsync(impact.VideoPlaylistIds, ct);
    }

    public async Task<PlaylistCleanupResult> RemovePlaylistItemsAsync(IEnumerable<int> videoPlaylistIds, CancellationToken ct = default)
    {
        var ids = videoPlaylistIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new PlaylistCleanupResult();
        }

        var rows = await _db.VideoPlaylists
            .Where(vp => ids.Contains(vp.Id))
            .Select(vp => new { vp.Id, vp.PlaylistId, vp.VideoId })
            .ToListAsync(ct);
        if (rows.Count == 0)
        {
            return new PlaylistCleanupResult();
        }

        var rowIds = rows.Select(row => row.Id).ToList();
        var entities = await _db.VideoPlaylists
            .Where(vp => rowIds.Contains(vp.Id))
            .ToListAsync(ct);
        _db.VideoPlaylists.RemoveRange(entities);
        await _db.SaveChangesAsync(ct);

        return new PlaylistCleanupResult
        {
            RemovedItemCount = rows.Count,
            AffectedPlaylistCount = rows.Select(row => row.PlaylistId).Distinct().Count(),
            AffectedVideoCount = rows.Select(row => row.VideoId).Distinct().Count()
        };
    }

    private async Task<PlaylistAccessImpact> BuildImpactAsync(
        IReadOnlyList<PlaylistEntryRecord> entries,
        AccessOverrides overrides,
        CancellationToken ct)
    {
        if (entries.Count == 0) return EmptyImpact();

        var accountIds = entries.Select(entry => entry.PlaylistAccountId).Distinct().ToList();
        var categoryIds = entries
            .Select(entry => ResolveCategoryId(entry, overrides))
            .Where(categoryId => categoryId.HasValue)
            .Select(categoryId => categoryId!.Value)
            .Distinct()
            .ToList();
        var activeSubscriptions = await GetActiveSubscriptionKeysAsync(accountIds, categoryIds, ct);

        foreach (var overrideItem in overrides.SubscriptionActiveOverrides)
        {
            if (overrideItem.Value)
            {
                activeSubscriptions.Add(overrideItem.Key);
            }
            else
            {
                activeSubscriptions.Remove(overrideItem.Key);
            }
        }

        var invalidEntries = entries
            .Where(entry => !IsEntryAccessible(entry, overrides, activeSubscriptions))
            .ToList();
        if (invalidEntries.Count == 0) return EmptyImpact();

        var affectedPlaylists = invalidEntries
            .GroupBy(entry => new
            {
                entry.PlaylistId,
                entry.PlaylistTitle,
                entry.PlaylistFilename,
                entry.PlaylistAccountId,
                entry.AccountName
            })
            .Select(group => new AffectedPlaylistItem
            {
                PlaylistId = group.Key.PlaylistId,
                Title = group.Key.PlaylistTitle,
                Filename = group.Key.PlaylistFilename,
                AccountId = group.Key.PlaylistAccountId,
                AccountName = group.Key.AccountName,
                RemovedItemCount = group.Count(),
                AffectedVideoCount = group.Select(entry => entry.VideoId).Distinct().Count()
            })
            .OrderBy(item => item.AccountName)
            .ThenBy(item => item.Title)
            .ThenBy(item => item.PlaylistId)
            .ToList();

        return new PlaylistAccessImpact
        {
            AffectedPlaylistCount = affectedPlaylists.Count,
            AffectedItemCount = invalidEntries.Count,
            AffectedVideoCount = invalidEntries.Select(entry => entry.VideoId).Distinct().Count(),
            AffectedPlaylists = affectedPlaylists,
            VideoPlaylistIds = invalidEntries.Select(entry => entry.VideoPlaylistId).Distinct().ToList()
        };
    }

    private async Task<HashSet<(int AccountId, int CategoryId)>> GetActiveSubscriptionKeysAsync(
        IReadOnlyCollection<int> accountIds,
        IReadOnlyCollection<int> categoryIds,
        CancellationToken ct)
    {
        if (accountIds.Count == 0 || categoryIds.Count == 0) return [];

        var now = _timeService.UtcNow;
        var subscriptions = await _db.Subscriptions
            .AsNoTracking()
            .Where(s => accountIds.Contains(s.AccountId)
                && categoryIds.Contains(s.CategoryId)
                && s.StartTime <= now
                && now <= s.EndTime)
            .Select(s => new { s.AccountId, s.CategoryId })
            .ToListAsync(ct);

        return subscriptions.Select(s => (s.AccountId, s.CategoryId)).ToHashSet();
    }

    private async Task<List<PlaylistEntryRecord>> LoadPlaylistEntriesAsync(
        Func<PlaylistEntryRecord, bool>? filter,
        CancellationToken ct)
    {
        var entries = await _db.VideoPlaylists
            .AsNoTracking()
            .Select(vp => new PlaylistEntryRecord(
                vp.Id,
                vp.VideoId,
                vp.Video.AccountId,
                vp.Video.CategoryId,
                vp.Video.Category != null && vp.Video.Category.Free,
                vp.PlaylistId,
                vp.Playlist.Title,
                vp.Playlist.Filename,
                vp.Playlist.AccountId,
                vp.Playlist.Account.Name))
            .ToListAsync(ct);

        return filter == null ? entries : entries.Where(filter).ToList();
    }

    private static bool IsVideoAccessible(
        VideoAccessRecord video,
        int accountId,
        IReadOnlySet<(int AccountId, int CategoryId)> activeSubscriptions)
    {
        if (video.AccountId.HasValue) return video.AccountId.Value == accountId;
        if (!video.CategoryId.HasValue) return true;
        return video.CategoryFree || activeSubscriptions.Contains((accountId, video.CategoryId.Value));
    }

    private static bool IsEntryAccessible(
        PlaylistEntryRecord entry,
        AccessOverrides overrides,
        IReadOnlySet<(int AccountId, int CategoryId)> activeSubscriptions)
    {
        if (entry.VideoAccountId.HasValue) return entry.VideoAccountId.Value == entry.PlaylistAccountId;

        var categoryId = ResolveCategoryId(entry, overrides);
        if (!categoryId.HasValue) return true;

        var categoryFree = ResolveCategoryFree(entry, categoryId.Value, overrides);
        return categoryFree || activeSubscriptions.Contains((entry.PlaylistAccountId, categoryId.Value));
    }

    private static int? ResolveCategoryId(PlaylistEntryRecord entry, AccessOverrides overrides) =>
        overrides.VideoCategoryOverrides.TryGetValue(entry.VideoId, out var categoryId)
            ? categoryId
            : entry.CategoryId;

    private static bool ResolveCategoryFree(PlaylistEntryRecord entry, int categoryId, AccessOverrides overrides) =>
        overrides.CategoryFreeOverrides.TryGetValue(categoryId, out var proposedFree)
            ? proposedFree
            : entry.CategoryId == categoryId && entry.CategoryFree;

    private static PlaylistAccessImpact EmptyImpact() => new();

    private sealed record VideoAccessRecord(int VideoId, int? AccountId, int? CategoryId, bool CategoryFree);

    private sealed record PlaylistEntryRecord(
        int VideoPlaylistId,
        int VideoId,
        int? VideoAccountId,
        int? CategoryId,
        bool CategoryFree,
        int PlaylistId,
        string PlaylistTitle,
        string PlaylistFilename,
        int PlaylistAccountId,
        string AccountName);

    private sealed class AccessOverrides
    {
        public Dictionary<int, int?> VideoCategoryOverrides { get; init; } = [];
        public Dictionary<int, bool> CategoryFreeOverrides { get; init; } = [];
        public Dictionary<(int AccountId, int CategoryId), bool> SubscriptionActiveOverrides { get; init; } = [];
    }
}
