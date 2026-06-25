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
using Microsoft.Net.Http.Headers;

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
    IPlaylistAccessService playlistAccessService,
    IVideoPlaybackTokenService videoPlaybackTokenService,
    AppDbContext db,
    ILogger<VideosController> logger) : MediaPiControllerBase(httpContextAccessor, db, logger)
{
    private readonly IUserInformationService _userInformationService = userInformationService;
    private readonly IVideoStorageService _videoStorageService = videoStorageService;
    private readonly IPlaylistAccessService _playlistAccessService = playlistAccessService;
    private readonly IVideoPlaybackTokenService _videoPlaybackTokenService = videoPlaybackTokenService;

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
        [FromQuery] int? availableForAccountId = null,
        CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        if (accountId == 0)
        {
            if (availableForAccountId.HasValue)
            {
                var playlistAccount = await _db.Accounts.AsNoTracking().AnyAsync(a => a.Id == availableForAccountId.Value, ct);
                if (!playlistAccount) return _404Account(availableForAccountId.Value);
                if (!_userInformationService.UserCanManageAccount(user, availableForAccountId.Value)) return _403();
            }

            var categoryError = await ValidateCategory(categoryId, ct);
            if (categoryError != null) return categoryError;

            var query = _db.Videos.AsNoTracking().Where(d => d.AccountId == null);
            if (categoryId.HasValue)
            {
                var normalizedCategoryId = NormalizeCategoryId(categoryId.Value);
                query = query.Where(v => v.CategoryId == normalizedCategoryId);
            }

            var videos = await query.Select(v => v.ToViewItem()).ToListAsync(ct);
            if (availableForAccountId.HasValue && videos.Count > 0)
            {
                var accessibleIds = await _playlistAccessService.GetAccessibleVideoIdsForAccountAsync(
                    availableForAccountId.Value,
                    videos.Select(v => v.Id),
                    ct);
                videos = videos.Where(v => accessibleIds.Contains(v.Id)).ToList();
            }

            return videos;
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

    [HttpPost("{id:int}/playback-token")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VideoPlaybackTokenViewItem))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<VideoPlaybackTokenViewItem>> CreatePlaybackToken(int id, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var video = await _db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);
        if (video == null) return _404Video(id);

        if (!_userInformationService.UserCanViewVideo(user, video.AccountId)) return _403();

        var token = _videoPlaybackTokenService.Generate(user.Id, video.Id);
        return Ok(new VideoPlaybackTokenViewItem
        {
            Token = token.Token,
            ExpiresAt = token.ExpiresAt,
            Url = BuildPlaybackUrl(video.Id, token.Token)
        });
    }

    [AllowAnonymous]
    [HttpGet("{id:int}/file")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> GetVideoFile(int id, [FromQuery] string? playbackToken = null, CancellationToken ct = default)
    {
        var user = await ResolvePlaybackUser(id, playbackToken, ct);
        if (user == null) return _401PlaybackToken();

        var video = await _db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);
        if (video == null) return _404Video(id);

        if (!_userInformationService.UserCanViewVideo(user, video.AccountId)) return _403();

        var path = _videoStorageService.GetAbsolutePath(video.Filename);
        var contentType = ResolveVideoContentType(video.OriginalFilename);
        Response.Headers[HeaderNames.ContentDisposition] = new ContentDispositionHeaderValue("inline")
        {
            FileNameStar = video.OriginalFilename
        }.ToString();
        return new PhysicalFileResult(path, contentType)
        {
            EnableRangeProcessing = true
        };
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
        var originalFilenameConflict = await ValidateOriginalFilenameAvailable(item.File.FileName, accountId, categoryId, null, ct);
        if (originalFilenameConflict != null) return originalFilenameConflict;

        var titleConflict = await ValidateVideoDescriptionAvailable(title, accountId, categoryId, null, ct);
        if (titleConflict != null) return titleConflict;

        var saveResult = await _videoStorageService.SaveVideoAsync(item.File, title, ct);

        // Check for duplicate filename before saving to database
        if (await _db.Videos.AnyAsync(v => v.Filename == saveResult.Filename, ct))
        {
            // Clean up the saved file since we can't use it
            await _videoStorageService.DeleteVideoAsync(saveResult.Filename, ct);
            return _409VideoFilename(saveResult.Filename);
        }

        var video = CreateVideo(title, saveResult, accountId, categoryId);

        try
        {
            _db.Videos.Add(video);
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" } pg
                                           && IsOriginalFilenameConstraint(pg.ConstraintName))
        {
            await CleanupSavedVideos([saveResult.Filename], ct);
            return _409VideoOriginalFilename(saveResult.OriginalFilename, accountId, categoryId);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" } pg
                                           && IsVideoDescriptionConstraint(pg.ConstraintName))
        {
            await CleanupSavedVideos([saveResult.Filename], ct);
            return _409VideoDescription(title, accountId, categoryId);
        }
        catch (DbUpdateException)
        {
            await CleanupSavedVideos([saveResult.Filename], ct);
            throw;
        }

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
        var duplicateBatchOriginalFilename = item.Files
            .Select(file => file.FileName)
            .GroupBy(filename => filename, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;
        if (duplicateBatchOriginalFilename != null)
        {
            return _409VideoOriginalFilename(duplicateBatchOriginalFilename, accountId, categoryId);
        }

        var existingOriginalFilenameConflict = await FindExistingOriginalFilenameConflict(
            item.Files.Select(file => file.FileName),
            accountId,
            categoryId,
            ct);
        if (existingOriginalFilenameConflict != null)
        {
            return _409VideoOriginalFilename(existingOriginalFilenameConflict, accountId, categoryId);
        }

        var duplicateBatchTitle = titles
            .GroupBy(title => title, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;
        if (duplicateBatchTitle != null)
        {
            return _409VideoDescription(duplicateBatchTitle, accountId, categoryId);
        }

        var existingTitleConflict = await FindExistingVideoDescriptionConflict(titles, accountId, categoryId, ct);
        if (existingTitleConflict != null)
        {
            return _409VideoDescription(existingTitleConflict, accountId, categoryId);
        }

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

                if (videosToCreate.Any(v => v.OriginalFilename == saveResult.OriginalFilename))
                {
                    await CleanupSavedVideos(savedFilenames, ct);
                    return _409VideoOriginalFilename(saveResult.OriginalFilename, accountId, categoryId);
                }

                if (videosToCreate.Any(v => v.Title == title))
                {
                    await CleanupSavedVideos(savedFilenames, ct);
                    return _409VideoDescription(title, accountId, categoryId);
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

        var nextTitle = item.Title?.Trim() ?? video.Title;
        var nextCategoryId = video.CategoryId;

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

            var normalizedCategoryId = NormalizeCategoryId(item.CategoryId.Value);
            PlaylistAccessImpact? impact = null;
            if (video.CategoryId != normalizedCategoryId)
            {
                var originalFilenameConflict = await ValidateOriginalFilenameAvailable(video.OriginalFilename, null, normalizedCategoryId, video.Id, ct);
                if (originalFilenameConflict != null) return originalFilenameConflict;

                var titleConflict = await ValidateVideoDescriptionAvailable(nextTitle, video.AccountId, normalizedCategoryId, video.Id, ct);
                if (titleConflict != null) return titleConflict;

                impact = await _playlistAccessService.BuildVideoCategoryChangeImpactAsync([video.Id], normalizedCategoryId, ct);
                if (impact.HasImpact && !item.ForcePlaylistCleanup)
                {
                    return StatusCode(StatusCodes.Status409Conflict, impact);
                }
            }

            video.CategoryId = normalizedCategoryId;
            if (item.ForcePlaylistCleanup && impact?.HasImpact == true)
            {
                await _playlistAccessService.RemovePlaylistItemsAsync(impact.VideoPlaylistIds, ct);
            }
        }

        if (item.Title != null && nextCategoryId == video.CategoryId)
        {
            var titleConflict = await ValidateVideoDescriptionAvailable(nextTitle, video.AccountId, nextCategoryId, video.Id, ct);
            if (titleConflict != null) return titleConflict;
        }

        if (item.Title != null)
        {
            video.Title = nextTitle;
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
        var videosToUpdate = new List<Video>();
        var acceptedOriginalFilenames = new HashSet<string>(StringComparer.Ordinal);
        var acceptedTitles = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in ids)
        {
            if (!videosById.TryGetValue(id, out var video))
            {
                result.Failures.Add(new VideoBatchOperationFailure
                {
                    Id = id,
                    Reason = "notFound",
                    Message = $"Видеофайл с ID {id} не найден"
                });
                continue;
            }

            if (!_userInformationService.UserCanManageVideo(user, video.AccountId))
            {
                result.Failures.Add(new VideoBatchOperationFailure
                {
                    Id = id,
                    Reason = "forbidden",
                    Message = $"Недостаточно прав для изменения видеофайла с ID {id}"
                });
                continue;
            }

            if (video.AccountId != null)
            {
                result.Failures.Add(new VideoBatchOperationFailure
                {
                    Id = id,
                    Reason = "accountLinked",
                    Message = $"Категория может быть назначена только общему видеофайлу; видео с ID {id} привязано к лицевому счёту"
                });
                continue;
            }

            if (acceptedOriginalFilenames.Contains(video.OriginalFilename)
                || await HasOriginalFilenameConflictAsync(video.OriginalFilename, null, normalizedCategoryId, video.Id, ct))
            {
                result.Failures.Add(DuplicateOriginalFilenameFailure(video));
                continue;
            }

            if (acceptedTitles.Contains(video.Title)
                || await HasVideoDescriptionConflictAsync(video.Title, null, normalizedCategoryId, video.Id, ct))
            {
                result.Failures.Add(DuplicateVideoDescriptionFailure(video));
                continue;
            }

            acceptedOriginalFilenames.Add(video.OriginalFilename);
            acceptedTitles.Add(video.Title);
            videosToUpdate.Add(video);
            result.UpdatedIds.Add(video.Id);
        }

        if (videosToUpdate.Count != 0)
        {
            var impact = await _playlistAccessService.BuildVideoCategoryChangeImpactAsync(
                videosToUpdate.Select(v => v.Id),
                normalizedCategoryId,
                ct);
            if (impact.HasImpact && !item.ForcePlaylistCleanup)
            {
                return StatusCode(StatusCodes.Status409Conflict, impact);
            }

            foreach (var video in videosToUpdate)
            {
                video.CategoryId = normalizedCategoryId;
            }

            if (item.ForcePlaylistCleanup && impact.HasImpact)
            {
                await _playlistAccessService.RemovePlaylistItemsAsync(impact.VideoPlaylistIds, ct);
            }
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
                    Message = $"Видеофайл с ID {id} не найден"
                });
                continue;
            }

            if (!_userInformationService.UserCanManageVideo(user, video.AccountId))
            {
                result.Failures.Add(new VideoBatchDeleteFailure
                {
                    Id = id,
                    Reason = "forbidden",
                    Message = $"Недостаточно прав для удаления видеофайла с ID {id}"
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
                    Message = $"Не удалось удалить файл видео с ID {video.Id}"
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

    private async Task<ObjectResult?> ValidateOriginalFilenameAvailable(
        string originalFilename,
        int? accountId,
        int? categoryId,
        int? excludedVideoId,
        CancellationToken ct)
    {
        return await HasOriginalFilenameConflictAsync(originalFilename, accountId, categoryId, excludedVideoId, ct)
            ? _409VideoOriginalFilename(originalFilename, accountId, categoryId)
            : null;
    }

    private async Task<string?> FindExistingOriginalFilenameConflict(
        IEnumerable<string> originalFilenames,
        int? accountId,
        int? categoryId,
        CancellationToken ct)
    {
        var orderedFilenames = originalFilenames
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (orderedFilenames.Count == 0) return null;

        var conflicts = await InVideoContainer(_db.Videos.AsNoTracking(), accountId, categoryId)
            .Where(v => orderedFilenames.Contains(v.OriginalFilename))
            .Select(v => v.OriginalFilename)
            .ToListAsync(ct);
        var conflictSet = conflicts.ToHashSet(StringComparer.Ordinal);

        return orderedFilenames.FirstOrDefault(conflictSet.Contains);
    }

    private async Task<ObjectResult?> ValidateVideoDescriptionAvailable(
        string title,
        int? accountId,
        int? categoryId,
        int? excludedVideoId,
        CancellationToken ct)
    {
        return await HasVideoDescriptionConflictAsync(title, accountId, categoryId, excludedVideoId, ct)
            ? _409VideoDescription(title, accountId, categoryId)
            : null;
    }

    private async Task<string?> FindExistingVideoDescriptionConflict(
        IEnumerable<string> titles,
        int? accountId,
        int? categoryId,
        CancellationToken ct)
    {
        var orderedTitles = titles
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (orderedTitles.Count == 0) return null;

        var conflicts = await InVideoContainer(_db.Videos.AsNoTracking(), accountId, categoryId)
            .Where(v => orderedTitles.Contains(v.Title))
            .Select(v => v.Title)
            .ToListAsync(ct);
        var conflictSet = conflicts.ToHashSet(StringComparer.Ordinal);

        return orderedTitles.FirstOrDefault(conflictSet.Contains);
    }

    private async Task<bool> HasOriginalFilenameConflictAsync(
        string originalFilename,
        int? accountId,
        int? categoryId,
        int? excludedVideoId,
        CancellationToken ct)
    {
        var query = InVideoContainer(_db.Videos.AsNoTracking(), accountId, categoryId)
            .Where(v => v.OriginalFilename == originalFilename);
        if (excludedVideoId.HasValue)
        {
            query = query.Where(v => v.Id != excludedVideoId.Value);
        }

        return await query.AnyAsync(ct);
    }

    private async Task<bool> HasVideoDescriptionConflictAsync(
        string title,
        int? accountId,
        int? categoryId,
        int? excludedVideoId,
        CancellationToken ct)
    {
        var query = InVideoContainer(_db.Videos.AsNoTracking(), accountId, categoryId)
            .Where(v => v.Title == title);
        if (excludedVideoId.HasValue)
        {
            query = query.Where(v => v.Id != excludedVideoId.Value);
        }

        return await query.AnyAsync(ct);
    }

    private static IQueryable<Video> InVideoContainer(IQueryable<Video> query, int? accountId, int? categoryId)
    {
        if (accountId.HasValue)
        {
            return query.Where(v => v.AccountId == accountId.Value);
        }

        if (categoryId.HasValue)
        {
            return query.Where(v => v.AccountId == null && v.CategoryId == categoryId.Value);
        }

        return query.Where(v => v.AccountId == null && v.CategoryId == null);
    }

    private VideoBatchOperationFailure DuplicateOriginalFilenameFailure(Video video)
    {
        return new VideoBatchOperationFailure
        {
            Id = video.Id,
            Reason = DuplicateOriginalFilenameReason,
            Message = VideoOriginalFilenameConflictMessage(video.OriginalFilename)
        };
    }

    private VideoBatchOperationFailure DuplicateVideoDescriptionFailure(Video video)
    {
        return new VideoBatchOperationFailure
        {
            Id = video.Id,
            Reason = DuplicateVideoDescriptionReason,
            Message = VideoDescriptionConflictMessage(video.Title)
        };
    }

    private static bool IsOriginalFilenameConstraint(string? constraintName) =>
        constraintName?.Contains("IX_videos_account_id_original_filename", StringComparison.OrdinalIgnoreCase) == true
        || constraintName?.Contains("IX_videos_category_id_original_filename", StringComparison.OrdinalIgnoreCase) == true
        || constraintName?.Contains("IX_videos_common_uncategorized_original_filename", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsVideoDescriptionConstraint(string? constraintName) =>
        constraintName?.Contains("IX_videos_account_id_title", StringComparison.OrdinalIgnoreCase) == true
        || constraintName?.Contains("IX_videos_category_id_title", StringComparison.OrdinalIgnoreCase) == true
        || constraintName?.Contains("IX_videos_common_uncategorized_title", StringComparison.OrdinalIgnoreCase) == true;

    private static int? NormalizeAccountId(int accountId) => accountId == 0 ? null : accountId;
    private static int? NormalizeCategoryId(int categoryId) => categoryId == 0 ? null : categoryId;
    private static int? ResolveCategoryId(int? categoryId) => categoryId.HasValue ? NormalizeCategoryId(categoryId.Value) : null;

    private static string ResolveVideoContentType(string filename) =>
        Path.GetExtension(filename).ToLowerInvariant() switch
        {
            ".mp4"  => "video/mp4",
            ".m4v"  => "video/x-m4v",
            ".webm" => "video/webm",
            ".ogv" or ".ogg" => "video/ogg",
            ".mov"  => "video/quicktime",
            ".avi"  => "video/x-msvideo",
            ".mkv"  => "video/x-matroska",
            _       => "application/octet-stream"
        };

    private static string BuildPlaybackUrl(int videoId, string token) =>
        $"/api/videos/{videoId}/file?playbackToken={Uri.EscapeDataString(token)}";

    private ObjectResult _401PlaybackToken()
    {
        return StatusCode(StatusCodes.Status401Unauthorized,
                          new ErrMessage { Msg = "Недействительная или просроченная ссылка на видеофайл" });
    }

    private async Task<User?> ResolvePlaybackUser(int videoId, string? playbackToken, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(playbackToken))
        {
            var userId = _videoPlaybackTokenService.Validate(playbackToken, videoId);
            if (userId.HasValue) return await LoadUser(userId.Value, ct);
        }

return _curUserId == 0 ? null : await CurrentUser();
    }

    private async Task<User?> LoadUser(int userId, CancellationToken ct)
    {
        return await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.UserAccounts)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
    }

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
