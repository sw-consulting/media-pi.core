// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.Collections.Generic;
using System.Linq;

using MediaPi.Core.Authorization;
using MediaPi.Core.Data;
using MediaPi.Core.Extensions;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaPi.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
public class VideosController(
    IHttpContextAccessor httpContextAccessor,
    IUserInformationService userInformationService,
    IVideoStorageService videoStorageService,
    AppDbContext db,
    ILogger<VideosController> logger) : MediaPiControllerBase(httpContextAccessor, db, logger)
{
    private readonly IUserInformationService _userInformationService = userInformationService;
    private readonly IVideoStorageService _videoStorageService = videoStorageService;

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<VideoViewItem>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<VideoViewItem>>> GetVideos(CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        IQueryable<Video> query = _db.Videos.AsNoTracking();

        if (user.IsAdministrator())
        {
            // Administrators can see all videos; no filtering is applied.
        }
        else if (user.IsManager())
        {
            var accountIds = _userInformationService.GetUserAccountIds(user);
            query = query.Where(v => v.AccountId == null || accountIds.Contains(v.AccountId.Value));
        }
        else
        {
            return _403();
        }

        var videos = await query.ToListAsync(ct);
        return videos.Select(v => v.ToViewItem()).ToList();
    }

    // GET: api/videos/by-account/{accountId}
    [HttpGet("by-account/{accountId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<VideoViewItem>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<VideoViewItem>>> GetVideosByAccount(
        int accountId,
        [FromQuery] int? categoryId = null,
        CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        if (accountId == 0)
        {
            var categoryError = await ValidateCategory(categoryId, ct);
            if (categoryError != null) return categoryError;

            var query = _db.Videos.AsNoTracking().Where(d => d.AccountId == null);
            if (categoryId.HasValue)
            {
                var normalizedCategoryId = NormalizeCategoryId(categoryId.Value);
                query = query.Where(v => v.CategoryId == normalizedCategoryId);
            }

            return await query.Select(v => v.ToViewItem()).ToListAsync(ct);
        }

        if (ResolveCategoryId(categoryId) != null) return _400VideoCategoryOnlyForCommon();

        if (!_userInformationService.UserCanViewVideo(user, accountId)) return _403();
        return await _db.Videos.AsNoTracking().Where(d => d.AccountId == accountId).Select(v => v.ToViewItem()).ToListAsync(ct);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VideoViewItem))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<VideoViewItem>> GetVideo(int id, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var video = await _db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);
        if (video == null) return _404Video(id);

        if (!_userInformationService.UserCanViewVideo(user, video.AccountId)) return _403();

        return video.ToViewItem();
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Reference))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<ActionResult<Reference>> UploadVideo([FromForm] VideoUploadItem item, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        if (item.File == null || item.File.Length == 0) return _400VideoFileMissing();
        if (string.IsNullOrWhiteSpace(item.Title)) return _400VideoTitleMissing();

        var validationError = await ValidateVideoUploadTarget(user, item.AccountId, item.CategoryId, ct);
        if (validationError != null) return validationError;

        var title = item.Title.Trim();
        var accountId = NormalizeAccountId(item.AccountId);
        var categoryId = ResolveCategoryId(item.CategoryId);
        var saveResult = await _videoStorageService.SaveVideoAsync(item.File, title, ct);

        // Check for duplicate filename before saving to database
        if (await _db.Videos.AnyAsync(v => v.Filename == saveResult.Filename, ct))
        {
            // Clean up the saved file since we can't use it
            await _videoStorageService.DeleteVideoAsync(saveResult.Filename, ct);
            return _409VideoFilename(saveResult.Filename);
        }

        var video = CreateVideo(title, saveResult, accountId, categoryId);

        _db.Videos.Add(video);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetVideo), new { id = video.Id }, new Reference { Id = video.Id });
    }

    [HttpPost("upload/batch")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(IEnumerable<Reference>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<Reference>>> UploadVideos([FromForm] VideoBatchUploadItem item, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        if (item.Files == null || item.Files.Count == 0 || item.Files.Any(file => file == null || file.Length == 0))
        {
            return _400VideoFileMissing();
        }

        var titles = item.Files.Select((file, index) => ResolveBatchUploadTitle(item, file, index)).ToList();
        if (titles.Any(string.IsNullOrWhiteSpace)) return _400VideoTitleMissing();

        var validationError = await ValidateVideoUploadTarget(user, item.AccountId, item.CategoryId, ct);
        if (validationError != null) return validationError;

        var accountId = NormalizeAccountId(item.AccountId);
        var categoryId = ResolveCategoryId(item.CategoryId);
        var savedFilenames = new List<string>();
        var videosToCreate = new List<Video>();

        try
        {
            for (var index = 0; index < item.Files.Count; index++)
            {
                var file = item.Files[index];
                var title = titles[index];
                var saveResult = await _videoStorageService.SaveVideoAsync(file, title, ct);
                savedFilenames.Add(saveResult.Filename);

                if (videosToCreate.Any(v => v.Filename == saveResult.Filename)
                    || await _db.Videos.AnyAsync(v => v.Filename == saveResult.Filename, ct))
                {
                    await CleanupSavedVideos(savedFilenames, ct);
                    return _409VideoFilename(saveResult.Filename);
                }

                videosToCreate.Add(CreateVideo(title, saveResult, accountId, categoryId));
            }

            _db.Videos.AddRange(videosToCreate);
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            await CleanupSavedVideos(savedFilenames, ct);
            throw;
        }

        var references = videosToCreate.Select(v => new Reference { Id = v.Id }).ToList();
        return StatusCode(StatusCodes.Status201Created, references);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdateVideo(int id, VideoUpdateItem item, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        if (item == null) return _400RequestPayloadMissing();
        if (item.Title != null && string.IsNullOrWhiteSpace(item.Title)) return _400VideoTitleMissing();

        var video = await _db.Videos
            .Include(v => v.VideoPlaylists)
            .FirstOrDefaultAsync(v => v.Id == id, ct);
        if (video == null) return _404Video(id);

        if (!_userInformationService.UserCanManageVideo(user, video.AccountId)) return _403();

        if (item.Title != null)
        {
            video.Title = item.Title.Trim();
        }

        if (item.PlaylistIds != null)
        {
            var (playlistIds, validationError) = await ValidateVideoPlaylists(item.PlaylistIds, video.AccountId ?? 0, ct);
            if (validationError != null) return validationError;

            ApplyVideoPlaylists(video, playlistIds);
        }

        if (item.CategoryId.HasValue)
        {
            var categoryError = await ValidateVideoCategoryUpdate(video, item.CategoryId.Value, ct);
            if (categoryError != null) return categoryError;

            video.CategoryId = NormalizeCategoryId(item.CategoryId.Value);
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("category/batch")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VideoBatchCategoryUpdateResult))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<VideoBatchCategoryUpdateResult>> UpdateVideoCategories(
        [FromBody] VideoBatchCategoryUpdateItem item,
        CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        if (item?.Ids == null || item.Ids.Count == 0) return _400RequestPayloadMissing();
        if (!item.CategoryId.HasValue) return _400VideoCategoryMissing();

        var categoryError = await ValidateCategory(item.CategoryId, ct);
        if (categoryError != null) return categoryError;

        var ids = item.Ids.Distinct().ToList();
        var result = new VideoBatchCategoryUpdateResult { RequestedCount = item.Ids.Count };

        var videos = await _db.Videos
            .Where(v => ids.Contains(v.Id))
            .ToListAsync(ct);
        var videosById = videos.ToDictionary(v => v.Id);
        var normalizedCategoryId = NormalizeCategoryId(item.CategoryId.Value);

        foreach (var id in ids)
        {
            if (!videosById.TryGetValue(id, out var video))
            {
                result.Failures.Add(new VideoBatchOperationFailure
                {
                    Id = id,
                    Reason = "notFound",
                    Message = $"Не удалось найти видеофайл [id={id}]"
                });
                continue;
            }

            if (!_userInformationService.UserCanManageVideo(user, video.AccountId))
            {
                result.Failures.Add(new VideoBatchOperationFailure
                {
                    Id = id,
                    Reason = "forbidden",
                    Message = $"Недостаточно прав для изменения видеофайла [id={id}]"
                });
                continue;
            }

            if (video.AccountId != null)
            {
                result.Failures.Add(new VideoBatchOperationFailure
                {
                    Id = id,
                    Reason = "accountLinked",
                    Message = $"Категория может быть назначена только общему видеофайлу [id={id}]"
                });
                continue;
            }

            video.CategoryId = normalizedCategoryId;
            result.UpdatedIds.Add(video.Id);
        }

        if (result.UpdatedIds.Count != 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return Ok(result);
    }

    [HttpPost("delete/batch")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VideoBatchDeleteResult))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<VideoBatchDeleteResult>> DeleteVideos([FromBody] VideoBatchDeleteItem item, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        if (item?.Ids == null || item.Ids.Count == 0) return _400RequestPayloadMissing();

        var ids = item.Ids.Distinct().ToList();
        var result = new VideoBatchDeleteResult { RequestedCount = item.Ids.Count };

        var videos = await _db.Videos
            .Include(v => v.VideoPlaylists)
            .Where(v => ids.Contains(v.Id))
            .ToListAsync(ct);
        var videosById = videos.ToDictionary(v => v.Id);
        var videosToDelete = new List<Video>();

        foreach (var id in ids)
        {
            if (!videosById.TryGetValue(id, out var video))
            {
                result.Failures.Add(new VideoBatchDeleteFailure
                {
                    Id = id,
                    Reason = "notFound",
                    Message = $"Не удалось найти видеофайл [id={id}]"
                });
                continue;
            }

            if (!_userInformationService.UserCanManageVideo(user, video.AccountId))
            {
                result.Failures.Add(new VideoBatchDeleteFailure
                {
                    Id = id,
                    Reason = "forbidden",
                    Message = $"Недостаточно прав для удаления видеофайла [id={id}]"
                });
                continue;
            }

            videosToDelete.Add(video);
        }

        if (videosToDelete.Count == 0) return Ok(result);

        var videosWithDeletedFiles = new List<Video>();

        foreach (var video in videosToDelete)
        {
            try
            {
                await _videoStorageService.DeleteVideoAsync(video.Filename, ct);
                videosWithDeletedFiles.Add(video);
                result.DeletedIds.Add(video.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to delete stored video file {Filename} for video {VideoId}", video.Filename, video.Id);
                result.Failures.Add(new VideoBatchDeleteFailure
                {
                    Id = video.Id,
                    Reason = "fileDeleteFailed",
                    Message = $"Не удалось удалить файл видео [id={video.Id}]"
                });
            }
        }

        if (videosWithDeletedFiles.Count > 0)
        {
            var videoPlaylistsToDelete = videosWithDeletedFiles.SelectMany(v => v.VideoPlaylists).ToList();
            if (videoPlaylistsToDelete.Count != 0)
            {
                _db.VideoPlaylists.RemoveRange(videoPlaylistsToDelete);
            }

            _db.Videos.RemoveRange(videosWithDeletedFiles);
            await _db.SaveChangesAsync(ct);
        }

        return Ok(result);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteVideo(int id, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var video = await _db.Videos
            .Include(v => v.VideoPlaylists)
            .FirstOrDefaultAsync(v => v.Id == id, ct);
        if (video == null) return _404Video(id);

        if (!_userInformationService.UserCanManageVideo(user, video.AccountId)) return _403();

        if (video.VideoPlaylists.Count != 0)
        {
            _db.VideoPlaylists.RemoveRange(video.VideoPlaylists);
        }

        _db.Videos.Remove(video);
        await _db.SaveChangesAsync(ct);

        await _videoStorageService.DeleteVideoAsync(video.Filename, ct);

        return NoContent();
    }

    private async Task<(List<int> PlaylistIds, ObjectResult? Error)> ValidateVideoPlaylists(IEnumerable<int> playlistIds, int accountId, CancellationToken ct)
    {
        var normalized = (playlistIds ?? Enumerable.Empty<int>()).Distinct().ToList();
        if (normalized.Count == 0) return (normalized, null);

        var playlists = await _db.Playlists
            .AsNoTracking()
            .Where(p => normalized.Contains(p.Id))
            .Select(p => new { p.Id, p.AccountId })
            .ToListAsync(ct);

        var foundIds = playlists.Select(p => p.Id).ToHashSet();
        if (foundIds.Count != normalized.Count)
        {
            var missingId = normalized.Except(foundIds).First();
            return (normalized, _404Playlist(missingId));
        }

        var mismatch = playlists.FirstOrDefault(p => p.AccountId != accountId);
        if (mismatch != null)
        {
            return (normalized, _400VideoPlaylistAccountMismatch(mismatch.Id, accountId));
        }

        return (normalized, null);
    }

    private static void ApplyVideoPlaylists(Video video, IReadOnlyCollection<int> playlistIds)
    {
        var desired = playlistIds.ToHashSet();

        var toRemove = video.VideoPlaylists.Where(vp => !desired.Contains(vp.PlaylistId)).ToList();
        if (toRemove.Count != 0)
        {
            foreach (var remove in toRemove)
            {
                video.VideoPlaylists.Remove(remove);
            }
        }

        var existing = video.VideoPlaylists.Select(vp => vp.PlaylistId).ToHashSet();
        foreach (var playlistId in desired.Except(existing))
        {
            video.VideoPlaylists.Add(new VideoPlaylist { VideoId = video.Id, PlaylistId = playlistId, Position = 0 });
        }
    }

    private async Task<ObjectResult?> ValidateVideoUploadTarget(User user, int accountId, int? categoryId, CancellationToken ct)
    {
        var normalizedAccountId = NormalizeAccountId(accountId);
        if (normalizedAccountId != null)
        {
            var account = await _db.Accounts.FindAsync([normalizedAccountId.Value], ct);
            if (account == null) return _404Account(normalizedAccountId.Value);

            if (ResolveCategoryId(categoryId) != null) return _400VideoCategoryOnlyForCommon();
        }

        if (!_userInformationService.UserCanManageAccount(user, accountId)) return _403();

        var categoryError = await ValidateCategory(categoryId, ct);
        if (categoryError != null) return categoryError;

        return null;
    }

    private static int? NormalizeAccountId(int accountId) => accountId == 0 ? null : accountId;
    private static int? NormalizeCategoryId(int categoryId) => categoryId == 0 ? null : categoryId;
    private static int? ResolveCategoryId(int? categoryId) => categoryId.HasValue ? NormalizeCategoryId(categoryId.Value) : null;

    private async Task<ObjectResult?> ValidateCategory(int? categoryId, CancellationToken ct)
    {
        if (!categoryId.HasValue || categoryId.Value == 0) return null;
        if (categoryId.Value < 0) return _404Category(categoryId.Value);

        var exists = await _db.Categories.AsNoTracking().AnyAsync(c => c.Id == categoryId.Value, ct);
        return exists ? null : _404Category(categoryId.Value);
    }

    private async Task<ObjectResult?> ValidateVideoCategoryUpdate(Video video, int categoryId, CancellationToken ct)
    {
        if (video.AccountId != null) return _400VideoCategoryOnlyForCommon();
        return await ValidateCategory(categoryId, ct);
    }

    private static string ResolveBatchUploadTitle(VideoBatchUploadItem item, IFormFile file, int index)
    {
        if (item.Titles.Count > index && !string.IsNullOrWhiteSpace(item.Titles[index]))
        {
            return item.Titles[index].Trim();
        }

        return file.FileName?.Trim() ?? string.Empty;
    }

    private static Video CreateVideo(string title, VideoSaveResult saveResult, int? accountId, int? categoryId)
    {
        return new Video
        {
            Title = title,
            Filename = saveResult.Filename,
            OriginalFilename = saveResult.OriginalFilename,
            FileSizeBytes = saveResult.FileSizeBytes,
            DurationSeconds = saveResult.DurationSeconds,
            AccountId = accountId,
            CategoryId = categoryId,
            Sha256 = saveResult.Sha256
        };
    }

    private async Task CleanupSavedVideos(IEnumerable<string> filenames, CancellationToken ct)
    {
        var uniqueFilenames = filenames
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var persistedFilenames = await _db.Videos
            .AsNoTracking()
            .Where(v => uniqueFilenames.Contains(v.Filename))
            .Select(v => v.Filename)
            .ToListAsync(ct);
        var persistedFilenameSet = persistedFilenames.ToHashSet(StringComparer.Ordinal);

        foreach (var filename in uniqueFilenames)
        {
            if (persistedFilenameSet.Contains(filename)) continue;

            try
            {
                await _videoStorageService.DeleteVideoAsync(filename, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup uploaded video file {Filename}", filename);
            }
        }
    }

}
