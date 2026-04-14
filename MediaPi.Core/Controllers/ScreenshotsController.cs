// Copyright (C) 2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Authorization;
using MediaPi.Core.Data;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaPi.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
public class ScreenshotsController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<ScreenshotsController> logger,
    IScreenshotStorageService screenshotStorageService,
    IUserInformationService userInformationService) : MediaPiControllerBase(httpContextAccessor, db, logger)
{
    private readonly IScreenshotStorageService _screenshotStorageService = screenshotStorageService;
    private readonly IUserInformationService _userInformationService = userInformationService;

    private const int MaxPageSize = 1000;
    private static readonly string[] ValidSortFields = ["id", "time_created"];
    private static readonly string[] ValidSortOrders = ["asc", "desc"];

    // GET: api/screenshots?deviceId={id}&from=...&to=...&page=1&pageSize=100&sortBy=id&sortOrder=asc
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<ScreenshotViewItem>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<PagedResult<ScreenshotViewItem>>> GetScreenshots(
        int deviceId,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 100,
        string sortBy = "id",
        string sortOrder = "asc",
        CancellationToken ct = default)
    {
        if (page <= 0 || pageSize <= 0 || pageSize > MaxPageSize)
            return _400();

        var normalizedSortBy = sortBy.ToLowerInvariant();
        var normalizedSortOrder = sortOrder.ToLowerInvariant();

        if (!ValidSortFields.Contains(normalizedSortBy))
            return _400();

        if (!ValidSortOrders.Contains(normalizedSortOrder))
            return _400();

        var device = await _db.Devices.FindAsync([deviceId], ct);
        if (device == null) return _404Device(deviceId);

        var user = await CurrentUser();
        if (user == null) return _403();

        if (!_userInformationService.UserCanViewDevice(user, device)) return _403();

        var query = _db.Screenshots
            .AsNoTracking()
            .Where(s => s.DeviceId == deviceId);

        if (from.HasValue)
            query = query.Where(s => s.TimeCreated >= from.Value);

        if (to.HasValue)
            query = query.Where(s => s.TimeCreated <= to.Value);

        query = (normalizedSortBy, normalizedSortOrder) switch
        {
            ("time_created", "desc") => query.OrderByDescending(s => s.TimeCreated).ThenByDescending(s => s.Id),
            ("time_created", _)      => query.OrderBy(s => s.TimeCreated).ThenBy(s => s.Id),
            (_, "desc")              => query.OrderByDescending(s => s.Id),
            _                        => query.OrderBy(s => s.Id)
        };

        var totalCount = await query.CountAsync(ct);
        var (actualPage, actualPageSize, _) = ComputePagination(page, pageSize, totalCount);

        var screenshots = await query
            .Skip((actualPage - 1) * actualPageSize)
            .Take(actualPageSize)
            .ToListAsync(ct);

        return Ok(new PagedResult<ScreenshotViewItem>
        {
            Items = screenshots.Select(s => new ScreenshotViewItem(s)).ToList(),
            Pagination = CreatePaginationInfo(page, pageSize, totalCount),
            Sorting = new SortingInfo { SortBy = normalizedSortBy, SortOrder = normalizedSortOrder }
        });
    }

    // GET: api/screenshots/{id}
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> GetScreenshot(int id, CancellationToken ct = default)
    {
        var screenshot = await _db.Screenshots
            .Include(s => s.Device)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        if (screenshot == null) return _404Screenshot(id);

        var user = await CurrentUser();
        if (user == null) return _403();

        if (!_userInformationService.UserCanViewDevice(user, screenshot.Device)) return _403();

        var path = _screenshotStorageService.GetAbsolutePath(screenshot.Filename);
        var contentType = ResolveContentType(screenshot.Filename);
        return PhysicalFile(path, contentType, screenshot.OriginalFilename);
    }

    // DELETE: api/screenshots/{id}
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteScreenshot(int id, CancellationToken ct = default)
    {
        var screenshot = await _db.Screenshots
            .Include(s => s.Device)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        if (screenshot == null) return _404Screenshot(id);

        var user = await CurrentUser();
        if (user == null) return _403();

        if (!_userInformationService.UserCanManageDeviceServices(user, screenshot.Device)) return _403();

        await _screenshotStorageService.DeleteScreenshotAsync(screenshot.Filename, ct);
        _db.Screenshots.Remove(screenshot);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    private static string ResolveContentType(string filename) =>
        Path.GetExtension(filename).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png"            => "image/png",
            ".gif"            => "image/gif",
            ".webp"           => "image/webp",
            _                 => "application/octet-stream"
        };
}
