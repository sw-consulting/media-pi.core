// Copyright (C) 2026 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MediaPi.Core.Controllers;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Controllers;

[TestFixture]
public class ScreenshotsControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private Mock<IScreenshotStorageService> _mockStorageService;
    private Mock<ILogger<ScreenshotsController>> _mockLogger;
    private ScreenshotsController _controller;
    private Device _device;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"screenshots_ctrl_test_{Guid.NewGuid()}")
            .Options;

        _dbContext = new AppDbContext(options);
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockStorageService = new Mock<IScreenshotStorageService>();
        _mockLogger = new Mock<ILogger<ScreenshotsController>>();

        _device = new Device { Id = 1, Name = "Cam", IpAddress = "10.0.0.1", Port = 8080 };
        _dbContext.Devices.Add(_device);
        _dbContext.SaveChanges();
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private void SetCurrentUser(int? userId)
    {
        var context = new DefaultHttpContext();
        if (userId.HasValue) context.Items["UserId"] = userId.Value;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);
        _controller = new ScreenshotsController(
            _mockHttpContextAccessor.Object,
            _dbContext,
            _mockLogger.Object,
            _mockStorageService.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    private Screenshot MakeScreenshot(int id, string filename, string originalFilename) => new()
    {
        Id = id,
        Filename = filename,
        OriginalFilename = originalFilename,
        FileSizeBytes = 2048,
        TimeCreated = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc),
        DeviceId = _device.Id,
        Device = _device
    };

    private static PagedResult<ScreenshotViewItem> UnwrapPagedResult(
        ActionResult<PagedResult<ScreenshotViewItem>> result)
    {
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var value = ((OkObjectResult)result.Result!).Value as PagedResult<ScreenshotViewItem>;
        Assert.That(value, Is.Not.Null);
        return value!;
    }

    #region GetScreenshot

    [Test]
    public async Task GetScreenshot_NotFound_Returns404()
    {
        SetCurrentUser(1);

        var result = await _controller.GetScreenshot(999, CancellationToken.None);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        Assert.That((obj.Value as ErrMessage)?.Msg, Does.Contain("999"));
    }

    [TestCase("0001/shot.jpg",  "image/jpeg")]
    [TestCase("0001/shot.jpeg", "image/jpeg")]
    [TestCase("0001/shot.png",  "image/png")]
    [TestCase("0001/shot.gif",  "image/gif")]
    [TestCase("0001/shot.webp", "image/webp")]
    [TestCase("0001/shot.bin",  "application/octet-stream")]
    public async Task GetScreenshot_ReturnsCorrectContentTypeForExtension(string filename, string expectedContentType)
    {
        var screenshot = MakeScreenshot(1, filename, "shot.jpg");
        _dbContext.Screenshots.Add(screenshot);
        _dbContext.SaveChanges();

        _mockStorageService.Setup(s => s.GetAbsolutePath(filename)).Returns($"/storage/{filename}");
        SetCurrentUser(1);

        var result = await _controller.GetScreenshot(1, CancellationToken.None);

        Assert.That(result, Is.TypeOf<PhysicalFileResult>());
        Assert.That(((PhysicalFileResult)result).ContentType, Is.EqualTo(expectedContentType));
    }

    [Test]
    public async Task GetScreenshot_ValidId_ReturnsCorrectPhysicalPathAndDownloadName()
    {
        var screenshot = MakeScreenshot(2, "0001/cam_2025-06-01_10-00-00.jpg", "cam_2025-06-01_10-00-00.jpg");
        _dbContext.Screenshots.Add(screenshot);
        _dbContext.SaveChanges();

        var expectedPath = "/storage/0001/cam_2025-06-01_10-00-00.jpg";
        _mockStorageService.Setup(s => s.GetAbsolutePath(screenshot.Filename)).Returns(expectedPath);
        SetCurrentUser(1);

        var result = await _controller.GetScreenshot(2, CancellationToken.None);

        Assert.That(result, Is.TypeOf<PhysicalFileResult>());
        var fileResult = (PhysicalFileResult)result;
        Assert.That(fileResult.FileName, Is.EqualTo(expectedPath));
        Assert.That(fileResult.FileDownloadName, Is.EqualTo(screenshot.OriginalFilename));
    }

    #endregion

    #region DeleteScreenshot

    [Test]
    public async Task DeleteScreenshot_NotFound_Returns404()
    {
        SetCurrentUser(1);

        var result = await _controller.DeleteScreenshot(999, CancellationToken.None);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        Assert.That((obj.Value as ErrMessage)?.Msg, Does.Contain("999"));
    }

    [Test]
    public async Task DeleteScreenshot_ExistingId_ReturnsNoContent()
    {
        var screenshot = MakeScreenshot(3, "0001/cam_2025-06-01_12-00-00.jpg", "cam_2025-06-01_12-00-00.jpg");
        _dbContext.Screenshots.Add(screenshot);
        _dbContext.SaveChanges();

        _mockStorageService
            .Setup(s => s.DeleteScreenshotAsync(screenshot.Filename, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        SetCurrentUser(1);

        var result = await _controller.DeleteScreenshot(3, CancellationToken.None);

        Assert.That(result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task DeleteScreenshot_ExistingId_CallsDeleteScreenshotAsyncWithCorrectFilename()
    {
        var screenshot = MakeScreenshot(4, "0001/cam_2025-06-01_13-00-00.jpg", "cam_2025-06-01_13-00-00.jpg");
        _dbContext.Screenshots.Add(screenshot);
        _dbContext.SaveChanges();

        _mockStorageService
            .Setup(s => s.DeleteScreenshotAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        SetCurrentUser(1);

        await _controller.DeleteScreenshot(4, CancellationToken.None);

        _mockStorageService.Verify(
            s => s.DeleteScreenshotAsync(screenshot.Filename, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task DeleteScreenshot_ExistingId_RemovesRecordFromDatabase()
    {
        var screenshot = MakeScreenshot(5, "0001/cam_2025-06-01_14-00-00.jpg", "cam_2025-06-01_14-00-00.jpg");
        _dbContext.Screenshots.Add(screenshot);
        _dbContext.SaveChanges();

        _mockStorageService
            .Setup(s => s.DeleteScreenshotAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        SetCurrentUser(1);

        await _controller.DeleteScreenshot(5, CancellationToken.None);

        Assert.That(await _dbContext.Screenshots.AnyAsync(s => s.Id == 5), Is.False);
    }

    #endregion

    #region GetScreenshots

    private IEnumerable<Screenshot> MakeSeedScreenshots(int deviceId, int count, DateTime baseTime)
    {
        return Enumerable.Range(1, count).Select(i => new Screenshot
        {
            Id = i,
            Filename = $"0001/cam_{i:D4}.jpg",
            OriginalFilename = $"cam_{i:D4}.jpg",
            FileSizeBytes = (uint)(1024 * i),
            TimeCreated = baseTime.AddMinutes(i),
            DeviceId = deviceId,
            Device = _device
        });
    }

    [Test]
    public async Task GetScreenshots_DeviceNotFound_Returns404()
    {
        SetCurrentUser(1);

        var result = await _controller.GetScreenshots(deviceId: 999, ct: CancellationToken.None);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)result.Result!).StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetScreenshots_InvalidPage_Returns400()
    {
        SetCurrentUser(1);

        var result = await _controller.GetScreenshots(deviceId: _device.Id, page: 0, ct: CancellationToken.None);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)result.Result!).StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(1001)]
    public async Task GetScreenshots_InvalidPageSize_Returns400(int badPageSize)
    {
        SetCurrentUser(1);

        var result = await _controller.GetScreenshots(deviceId: _device.Id, pageSize: badPageSize, ct: CancellationToken.None);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)result.Result!).StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetScreenshots_InvalidSortBy_Returns400()
    {
        SetCurrentUser(1);

        var result = await _controller.GetScreenshots(deviceId: _device.Id, sortBy: "invalid_field", ct: CancellationToken.None);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)result.Result!).StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetScreenshots_NoScreenshots_ReturnsEmptyPage()
    {
        SetCurrentUser(1);

        var result = await _controller.GetScreenshots(deviceId: _device.Id, ct: CancellationToken.None);

        var paged = UnwrapPagedResult(result);
        Assert.That(paged.Items, Is.Empty);
        Assert.That(paged.Pagination.TotalCount, Is.EqualTo(0));
    }

    [Test]
    public async Task GetScreenshots_ScopedToDevice_ExcludesOtherDevices()
    {
        var otherDevice = new Device { Id = 2, Name = "Other", IpAddress = "10.0.0.2", Port = 8080 };
        _dbContext.Devices.Add(otherDevice);
        _dbContext.Screenshots.Add(MakeScreenshot(10, "0001/own.jpg",   "own.jpg")   );
        _dbContext.Screenshots.Add(new Screenshot
        {
            Id = 11, Filename = "0001/other.jpg", OriginalFilename = "other.jpg",
            FileSizeBytes = 512, TimeCreated = DateTime.UtcNow,
            DeviceId = otherDevice.Id, Device = otherDevice
        });
        _dbContext.SaveChanges();
        SetCurrentUser(1);

        var result = await _controller.GetScreenshots(deviceId: _device.Id, ct: CancellationToken.None);

        var paged = UnwrapPagedResult(result);
        Assert.That(paged.Items.Count(), Is.EqualTo(1));
        Assert.That(paged.Items.First().Id, Is.EqualTo(10));
    }

    [Test]
    public async Task GetScreenshots_DefaultSort_ReturnsByIdAscending()
    {
        var baseTime = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        _dbContext.Screenshots.AddRange(MakeSeedScreenshots(_device.Id, 3, baseTime));
        _dbContext.SaveChanges();
        SetCurrentUser(1);

        var result = await _controller.GetScreenshots(deviceId: _device.Id, ct: CancellationToken.None);

        var ids = UnwrapPagedResult(result).Items.Select(s => s.Id).ToList();
        Assert.That(ids, Is.EqualTo(ids.OrderBy(x => x).ToList()));
    }

    [Test]
    public async Task GetScreenshots_SortByIdDesc_ReturnsDescendingOrder()
    {
        var baseTime = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        _dbContext.Screenshots.AddRange(MakeSeedScreenshots(_device.Id, 3, baseTime));
        _dbContext.SaveChanges();
        SetCurrentUser(1);

        var result = await _controller.GetScreenshots(deviceId: _device.Id, sortBy: "id", sortOrder: "desc", ct: CancellationToken.None);

        var ids = UnwrapPagedResult(result).Items.Select(s => s.Id).ToList();
        Assert.That(ids, Is.EqualTo(ids.OrderByDescending(x => x).ToList()));
    }

    [Test]
    public async Task GetScreenshots_SortByTimeCreatedAsc_ReturnsAscendingOrder()
    {
        var baseTime = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        _dbContext.Screenshots.AddRange(MakeSeedScreenshots(_device.Id, 3, baseTime));
        _dbContext.SaveChanges();
        SetCurrentUser(1);

        var result = await _controller.GetScreenshots(deviceId: _device.Id, sortBy: "time_created", sortOrder: "asc", ct: CancellationToken.None);

        var times = UnwrapPagedResult(result).Items.Select(s => s.TimeCreated).ToList();
        Assert.That(times, Is.EqualTo(times.OrderBy(x => x).ToList()));
    }

    [Test]
    public async Task GetScreenshots_SortByTimeCreatedDesc_ReturnsDescendingOrder()
    {
        var baseTime = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        _dbContext.Screenshots.AddRange(MakeSeedScreenshots(_device.Id, 3, baseTime));
        _dbContext.SaveChanges();
        SetCurrentUser(1);

        var result = await _controller.GetScreenshots(deviceId: _device.Id, sortBy: "time_created", sortOrder: "desc", ct: CancellationToken.None);

        var times = UnwrapPagedResult(result).Items.Select(s => s.TimeCreated).ToList();
        Assert.That(times, Is.EqualTo(times.OrderByDescending(x => x).ToList()));
    }

    [Test]
    public async Task GetScreenshots_FilterFrom_ExcludesEarlierScreenshots()
    {
        var baseTime = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        _dbContext.Screenshots.AddRange(MakeSeedScreenshots(_device.Id, 5, baseTime));
        _dbContext.SaveChanges();
        SetCurrentUser(1);

        // baseTime + 3 minutes → items 3, 4, 5
        var result = await _controller.GetScreenshots(deviceId: _device.Id, from: baseTime.AddMinutes(3), ct: CancellationToken.None);

        var paged = UnwrapPagedResult(result);
        Assert.That(paged.Pagination.TotalCount, Is.EqualTo(3));
        Assert.That(paged.Items.All(s => s.TimeCreated >= baseTime.AddMinutes(3)), Is.True);
    }

    [Test]
    public async Task GetScreenshots_FilterTo_ExcludesLaterScreenshots()
    {
        var baseTime = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        _dbContext.Screenshots.AddRange(MakeSeedScreenshots(_device.Id, 5, baseTime));
        _dbContext.SaveChanges();
        SetCurrentUser(1);

        // baseTime + 3 minutes → items 1, 2, 3
        var result = await _controller.GetScreenshots(deviceId: _device.Id, to: baseTime.AddMinutes(3), ct: CancellationToken.None);

        var paged = UnwrapPagedResult(result);
        Assert.That(paged.Pagination.TotalCount, Is.EqualTo(3));
        Assert.That(paged.Items.All(s => s.TimeCreated <= baseTime.AddMinutes(3)), Is.True);
    }

    [Test]
    public async Task GetScreenshots_FilterFromAndTo_ReturnsRangeOnly()
    {
        var baseTime = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        _dbContext.Screenshots.AddRange(MakeSeedScreenshots(_device.Id, 5, baseTime));
        _dbContext.SaveChanges();
        SetCurrentUser(1);

        // items 2 and 3 fall within [+2min, +3min]
        var result = await _controller.GetScreenshots(
            deviceId: _device.Id,
            from: baseTime.AddMinutes(2),
            to: baseTime.AddMinutes(3),
            ct: CancellationToken.None);

        Assert.That(UnwrapPagedResult(result).Pagination.TotalCount, Is.EqualTo(2));
    }

    [Test]
    public async Task GetScreenshots_Pagination_ReturnsCorrectPage()
    {
        var baseTime = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        _dbContext.Screenshots.AddRange(MakeSeedScreenshots(_device.Id, 5, baseTime));
        _dbContext.SaveChanges();
        SetCurrentUser(1);

        var page1 = UnwrapPagedResult(await _controller.GetScreenshots(deviceId: _device.Id, page: 1, pageSize: 2, ct: CancellationToken.None));
        var page2 = UnwrapPagedResult(await _controller.GetScreenshots(deviceId: _device.Id, page: 2, pageSize: 2, ct: CancellationToken.None));

        Assert.That(page1.Items.Count(), Is.EqualTo(2));
        Assert.That(page2.Items.Count(), Is.EqualTo(2));
        Assert.That(page1.Items.Select(s => s.Id).Intersect(page2.Items.Select(s => s.Id)), Is.Empty);
    }

    [Test]
    public async Task GetScreenshots_Pagination_PopulatesPaginationInfo()
    {
        var baseTime = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        _dbContext.Screenshots.AddRange(MakeSeedScreenshots(_device.Id, 5, baseTime));
        _dbContext.SaveChanges();
        SetCurrentUser(1);

        var result = await _controller.GetScreenshots(deviceId: _device.Id, page: 1, pageSize: 2, ct: CancellationToken.None);

        var p = UnwrapPagedResult(result).Pagination;
        Assert.That(p.TotalCount, Is.EqualTo(5));
        Assert.That(p.TotalPages, Is.EqualTo(3));
        Assert.That(p.CurrentPage, Is.EqualTo(1));
        Assert.That(p.HasNextPage, Is.True);
        Assert.That(p.HasPreviousPage, Is.False);
    }

    [Test]
    public async Task GetScreenshots_SortingInfo_ReflectsRequestedParameters()
    {
        SetCurrentUser(1);

        var result = await _controller.GetScreenshots(deviceId: _device.Id, sortBy: "time_created", sortOrder: "desc", ct: CancellationToken.None);

        var paged = UnwrapPagedResult(result);
        Assert.That(paged.Sorting.SortBy, Is.EqualTo("time_created"));
        Assert.That(paged.Sorting.SortOrder, Is.EqualTo("desc"));
    }

    [Test]
    public async Task GetScreenshots_ViewItemFields_MappedCorrectly()
    {
        var expected = MakeScreenshot(20, "0001/cam_shot.jpg", "cam_shot.jpg");
        _dbContext.Screenshots.Add(expected);
        _dbContext.SaveChanges();
        SetCurrentUser(1);

        var result = await _controller.GetScreenshots(deviceId: _device.Id, ct: CancellationToken.None);

        var item = UnwrapPagedResult(result).Items.Single();
        Assert.That(item.Id,               Is.EqualTo(expected.Id));
        Assert.That(item.Filename,         Is.EqualTo(expected.Filename));
        Assert.That(item.OriginalFilename, Is.EqualTo(expected.OriginalFilename));
        Assert.That(item.FileSizeBytes,    Is.EqualTo(expected.FileSizeBytes));
        Assert.That(item.TimeCreated,      Is.EqualTo(expected.TimeCreated));
        Assert.That(item.DeviceId,         Is.EqualTo(expected.DeviceId));
    }

    #endregion
}
