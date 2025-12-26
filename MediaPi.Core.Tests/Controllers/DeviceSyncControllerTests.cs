// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.Linq;
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
public class DeviceSyncControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private Mock<IVideoStorageService> _mockVideoStorageService;
    private Mock<ILogger<DeviceSyncController>> _mockLogger;
    private DeviceSyncController _controller;
    private Account _account;
    private DeviceGroup _deviceGroup;
    private Device _device;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"device_sync_controller_test_db_{Guid.NewGuid()}")
            .Options;

        _dbContext = new AppDbContext(options);
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockVideoStorageService = new Mock<IVideoStorageService>();
        _mockLogger = new Mock<ILogger<DeviceSyncController>>();

        _account = new Account { Id = 1, Name = "Account" };
        _deviceGroup = new DeviceGroup { Id = 1, Name = "Group", AccountId = _account.Id, Account = _account };
        _device = new Device { Id = 1, Name = "Device", IpAddress = "127.0.0.1", Port = 8080, DeviceGroupId = _deviceGroup.Id, DeviceGroup = _deviceGroup };

        _dbContext.Accounts.Add(_account);
        _dbContext.DeviceGroups.Add(_deviceGroup);
        _dbContext.Devices.Add(_device);
        _dbContext.SaveChanges();
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private void SetDeviceContext(int? deviceId)
    {
        var context = new DefaultHttpContext();
        if (deviceId.HasValue)
        {
            context.Items["DeviceId"] = deviceId.Value;
        }

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);
        _controller = new DeviceSyncController(
            _mockHttpContextAccessor.Object,
            _mockVideoStorageService.Object,
            _dbContext,
            _mockLogger.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    [Test]
    public async Task GetManifest_ReturnsDistinctVideosForDeviceGroup()
    {
        var playlist1 = new Playlist { Id = 1, Title = "Playlist 1", Filename = "playlist1.json", AccountId = _account.Id, Account = _account };
        var playlist2 = new Playlist { Id = 2, Title = "Playlist 2", Filename = "playlist2.json", AccountId = _account.Id, Account = _account };
        var video1 = new Video { Id = 1, Title = "Video 1", Filename = "0001/video1.mp4", OriginalFilename = "video1.mp4", FileSizeBytes = 1024, AccountId = _account.Id, Account = _account, Sha256 = "abc" };
        var video2 = new Video { Id = 2, Title = "Video 2", Filename = "0001/video2.mp4", OriginalFilename = "video2.mp4", FileSizeBytes = 2048, AccountId = _account.Id, Account = _account, Sha256 = "def" };

        _dbContext.Playlists.AddRange(playlist1, playlist2);
        _dbContext.Videos.AddRange(video1, video2);
        _dbContext.VideoPlaylists.AddRange(
            new VideoPlaylist { VideoId = video1.Id, Video = video1, PlaylistId = playlist1.Id, Playlist = playlist1 },
            new VideoPlaylist { VideoId = video2.Id, Video = video2, PlaylistId = playlist1.Id, Playlist = playlist1 },
            new VideoPlaylist { VideoId = video1.Id, Video = video1, PlaylistId = playlist2.Id, Playlist = playlist2 });
        _dbContext.PlaylistDeviceGroups.AddRange(
            new PlaylistDeviceGroup { PlaylistId = playlist1.Id, Playlist = playlist1, DeviceGroupId = _deviceGroup.Id, DeviceGroup = _deviceGroup },
            new PlaylistDeviceGroup { PlaylistId = playlist2.Id, Playlist = playlist2, DeviceGroupId = _deviceGroup.Id, DeviceGroup = _deviceGroup });
        _dbContext.SaveChanges();

        SetDeviceContext(_device.Id);

        var result = await _controller.GetManifest();

        Assert.That(result.Value, Is.Not.Null);
        var manifest = result.Value!.ToList();
        Assert.That(manifest, Has.Count.EqualTo(2));
        Assert.That(manifest.Select(m => m.Id), Is.EquivalentTo(new[] { video1.Id, video2.Id }));

        var video1Item = manifest.Single(m => m.Id == video1.Id);
        Assert.That(video1Item.Filename, Is.EqualTo(video1.Filename));
        Assert.That(video1Item.FileSizeBytes, Is.EqualTo(video1.FileSizeBytes));
        Assert.That(video1Item.Sha256, Is.EqualTo(video1.Sha256));
    }

    [Test]
    public async Task GetManifest_NoDeviceGroup_ReturnsEmpty()
    {
        var deviceWithoutGroup = new Device { Id = 2, Name = "NoGroup", IpAddress = "127.0.0.2", Port = 8081 };
        _dbContext.Devices.Add(deviceWithoutGroup);
        _dbContext.SaveChanges();

        SetDeviceContext(deviceWithoutGroup.Id);

        var result = await _controller.GetManifest();

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!, Is.Empty);
    }

    [Test]
    public async Task Download_ReturnsPhysicalFileResult()
    {
        var video = new Video
        {
            Id = 10,
            Title = "Video",
            Filename = "0001/video.mp4",
            OriginalFilename = "video.mp4",
            FileSizeBytes = 1024,
            AccountId = _account.Id,
            Account = _account
        };
        _dbContext.Videos.Add(video);
        _dbContext.SaveChanges();

        var expectedPath = "/videos/0001/video.mp4";
        _mockVideoStorageService.Setup(s => s.GetAbsolutePath(video.Filename)).Returns(expectedPath);

        SetDeviceContext(_device.Id);

        var result = await _controller.Download(video.Id);

        Assert.That(result, Is.TypeOf<PhysicalFileResult>());
        var fileResult = (PhysicalFileResult)result;
        Assert.That(fileResult.FileName, Is.EqualTo(expectedPath));
        Assert.That(fileResult.FileDownloadName, Is.EqualTo(video.OriginalFilename));
        Assert.That(fileResult.ContentType, Is.EqualTo("application/octet-stream"));
    }

    [Test]
    public async Task GetManifest_MissingSha256_Returns500()
    {
        var playlist = new Playlist { Id = 3, Title = "Playlist 3", Filename = "playlist3.json", AccountId = _account.Id, Account = _account };
        var video = new Video
        {
            Id = 20,
            Title = "Video 3",
            Filename = "0001/video3.mp4",
            OriginalFilename = "video3.mp4",
            FileSizeBytes = 1024,
            AccountId = _account.Id,
            Account = _account,
            Sha256 = null
        };

        _dbContext.Playlists.Add(playlist);
        _dbContext.Videos.Add(video);
        _dbContext.VideoPlaylists.Add(new VideoPlaylist { VideoId = video.Id, Video = video, PlaylistId = playlist.Id, Playlist = playlist });
        _dbContext.PlaylistDeviceGroups.Add(new PlaylistDeviceGroup { PlaylistId = playlist.Id, Playlist = playlist, DeviceGroupId = _deviceGroup.Id, DeviceGroup = _deviceGroup });
        _dbContext.SaveChanges();

        SetDeviceContext(_device.Id);

        var result = await _controller.GetManifest();

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result.Result!;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
    }
}
