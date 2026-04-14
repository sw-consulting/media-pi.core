// Copyright (C) 2025-2026 sw.consulting
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
        var playlist = new Playlist { Id = 10, Title = "Download Playlist", Filename = "download_playlist.json", AccountId = _account.Id, Account = _account };
        var video = new Video
        {
            Id = 10,
            Title = "Video",
            Filename = "0001/video.mp4",
            OriginalFilename = "video.mp4",
            FileSizeBytes = 1024,
            AccountId = _account.Id,
            Account = _account,
            Sha256 = "abc"
        };
        
        _dbContext.Playlists.Add(playlist);
        _dbContext.Videos.Add(video);
        _dbContext.VideoPlaylists.Add(new VideoPlaylist { VideoId = video.Id, Video = video, PlaylistId = playlist.Id, Playlist = playlist });
        _dbContext.PlaylistDeviceGroups.Add(new PlaylistDeviceGroup { PlaylistId = playlist.Id, Playlist = playlist, DeviceGroupId = _deviceGroup.Id, DeviceGroup = _deviceGroup });
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
        
        var errMessage = obj.Value as ErrMessage;
        Assert.That(errMessage, Is.Not.Null);
        Assert.That(errMessage!.Msg, Does.Contain("sha256"));
        Assert.That(errMessage.Msg, Does.Contain($"id={video.Id}"));
    }

    [Test]
    public async Task GetManifest_MissingFilename_Returns500()
    {
        var playlist = new Playlist { Id = 4, Title = "Playlist 4", Filename = "playlist4.json", AccountId = _account.Id, Account = _account };
        var video = new Video
        {
            Id = 30,
            Title = "Video 4",
            Filename = "",
            OriginalFilename = "video4.mp4",
            FileSizeBytes = 1024,
            AccountId = _account.Id,
            Account = _account,
            Sha256 = "xyz123"
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
        
        var errMessage = obj.Value as ErrMessage;
        Assert.That(errMessage, Is.Not.Null);
        Assert.That(errMessage!.Msg, Does.Contain("filename"));
        Assert.That(errMessage.Msg, Does.Contain($"id={video.Id}"));
    }

    [Test]
    public async Task Download_VideoNotFound_Returns404()
    {
        SetDeviceContext(_device.Id);

        var result = await _controller.Download(999);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task Download_VideoNotInDeviceGroup_Returns403()
    {
        var otherGroup = new DeviceGroup { Id = 2, Name = "Other Group", AccountId = _account.Id, Account = _account };
        _dbContext.DeviceGroups.Add(otherGroup);
        
        var playlist = new Playlist { Id = 5, Title = "Playlist 5", Filename = "playlist5.json", AccountId = _account.Id, Account = _account };
        var video = new Video
        {
            Id = 40,
            Title = "Video 5",
            Filename = "0001/video5.mp4",
            OriginalFilename = "video5.mp4",
            FileSizeBytes = 1024,
            AccountId = _account.Id,
            Account = _account,
            Sha256 = "abc456"
        };

        _dbContext.Playlists.Add(playlist);
        _dbContext.Videos.Add(video);
        _dbContext.VideoPlaylists.Add(new VideoPlaylist { VideoId = video.Id, Video = video, PlaylistId = playlist.Id, Playlist = playlist });
        _dbContext.PlaylistDeviceGroups.Add(new PlaylistDeviceGroup { PlaylistId = playlist.Id, Playlist = playlist, DeviceGroupId = otherGroup.Id, DeviceGroup = otherGroup });
        _dbContext.SaveChanges();

        SetDeviceContext(_device.Id);

        var result = await _controller.Download(video.Id);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task Download_DeviceWithoutGroup_Returns403()
    {
        var deviceWithoutGroup = new Device { Id = 3, Name = "NoGroup", IpAddress = "127.0.0.3", Port = 8082 };
        _dbContext.Devices.Add(deviceWithoutGroup);
        
        var video = new Video
        {
            Id = 50,
            Title = "Video 6",
            Filename = "0001/video6.mp4",
            OriginalFilename = "video6.mp4",
            FileSizeBytes = 1024,
            AccountId = _account.Id,
            Account = _account,
            Sha256 = "xyz789"
        };
        _dbContext.Videos.Add(video);
        _dbContext.SaveChanges();

        SetDeviceContext(deviceWithoutGroup.Id);

        var result = await _controller.Download(video.Id);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DownloadPlaylist_DeviceWithoutGroup_ReturnsNoContent()
    {
        var deviceWithoutGroup = new Device { Id = 4, Name = "NoGroup", IpAddress = "127.0.0.4", Port = 8083 };
        _dbContext.Devices.Add(deviceWithoutGroup);
        _dbContext.SaveChanges();

        SetDeviceContext(deviceWithoutGroup.Id);

        var result = await _controller.DownloadPlaylist();

        Assert.That(result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task DownloadPlaylist_NoPlaylistWithPlayTrue_ReturnsNoContent()
    {
        var playlist = new Playlist { Id = 6, Title = "Playlist 6", Filename = "playlist6.json", AccountId = _account.Id, Account = _account };
        _dbContext.Playlists.Add(playlist);
        _dbContext.PlaylistDeviceGroups.Add(new PlaylistDeviceGroup 
        { 
            PlaylistId = playlist.Id, 
            Playlist = playlist, 
            DeviceGroupId = _deviceGroup.Id, 
            DeviceGroup = _deviceGroup,
            Play = false 
        });
        _dbContext.SaveChanges();

        SetDeviceContext(_device.Id);

        var result = await _controller.DownloadPlaylist();

        Assert.That(result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task DownloadPlaylist_PlaylistWithPlayTrue_ReturnsM3uFile()
    {
        var playlist = new Playlist { Id = 7, Title = "Playlist 7", Filename = "playlist7.json", AccountId = _account.Id, Account = _account };
        var video1 = new Video { Id = 60, Title = "Video 7", Filename = "0001/video7.mp4", OriginalFilename = "video7.mp4", FileSizeBytes = 1024, AccountId = _account.Id, Account = _account, Sha256 = "aaa" };
        var video2 = new Video { Id = 61, Title = "Video 8", Filename = "0001/video8.mp4", OriginalFilename = "video8.mp4", FileSizeBytes = 2048, AccountId = _account.Id, Account = _account, Sha256 = "bbb" };

        _dbContext.Playlists.Add(playlist);
        _dbContext.Videos.AddRange(video1, video2);
        _dbContext.VideoPlaylists.AddRange(
            new VideoPlaylist { VideoId = video1.Id, Video = video1, PlaylistId = playlist.Id, Playlist = playlist, Position = 1 },
            new VideoPlaylist { VideoId = video2.Id, Video = video2, PlaylistId = playlist.Id, Playlist = playlist, Position = 0 });
        _dbContext.PlaylistDeviceGroups.Add(new PlaylistDeviceGroup 
        { 
            PlaylistId = playlist.Id, 
            Playlist = playlist, 
            DeviceGroupId = _deviceGroup.Id, 
            DeviceGroup = _deviceGroup,
            Play = true 
        });
        _dbContext.SaveChanges();

        SetDeviceContext(_device.Id);

        var result = await _controller.DownloadPlaylist();

        Assert.That(result, Is.TypeOf<FileContentResult>());
        var fileResult = (FileContentResult)result;
        Assert.That(fileResult.ContentType, Is.EqualTo("text/plain"));
        Assert.That(fileResult.FileDownloadName, Is.EqualTo("playlist.m3u"));

        var content = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);
        Assert.That(content, Does.StartWith("#EXTM3U"));
        Assert.That(content, Does.Contain("0001/video8.mp4")); // Position 0 should be first
        Assert.That(content, Does.Contain("0001/video7.mp4")); // Position 1 should be second
        
        // Verify ordering
        var video8Index = content.IndexOf("0001/video8.mp4");
        var video7Index = content.IndexOf("0001/video7.mp4");
        Assert.That(video8Index, Is.LessThan(video7Index), "Videos should be ordered by position");
    }

    [Test]
    public async Task DownloadPlaylist_DeviceNotFound_Returns404()
    {
        SetDeviceContext(999);

        var result = await _controller.DownloadPlaylist();

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task DownloadPlaylist_DeviceIdMissing_Returns500()
    {
        SetDeviceContext(null);

        var result = await _controller.DownloadPlaylist();

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
    }

    [Test]
    public async Task DownloadPlaylist_MultiplePlaylistsOneWithPlayTrue_ReturnsCorrectM3u()
    {
        var playlist1 = new Playlist { Id = 8, Title = "Playlist 8", Filename = "playlist8.json", AccountId = _account.Id, Account = _account };
        var playlist2 = new Playlist { Id = 9, Title = "Playlist 9", Filename = "playlist9.json", AccountId = _account.Id, Account = _account };
        var video1 = new Video { Id = 70, Title = "Video 9", Filename = "0001/video9.mp4", OriginalFilename = "video9.mp4", FileSizeBytes = 1024, AccountId = _account.Id, Account = _account, Sha256 = "ccc" };
        var video2 = new Video { Id = 71, Title = "Video 10", Filename = "0001/video10.mp4", OriginalFilename = "video10.mp4", FileSizeBytes = 2048, AccountId = _account.Id, Account = _account, Sha256 = "ddd" };

        _dbContext.Playlists.AddRange(playlist1, playlist2);
        _dbContext.Videos.AddRange(video1, video2);
        _dbContext.VideoPlaylists.AddRange(
            new VideoPlaylist { VideoId = video1.Id, Video = video1, PlaylistId = playlist1.Id, Playlist = playlist1, Position = 0 },
            new VideoPlaylist { VideoId = video2.Id, Video = video2, PlaylistId = playlist2.Id, Playlist = playlist2, Position = 0 });
        _dbContext.PlaylistDeviceGroups.AddRange(
            new PlaylistDeviceGroup { PlaylistId = playlist1.Id, Playlist = playlist1, DeviceGroupId = _deviceGroup.Id, DeviceGroup = _deviceGroup, Play = false },
            new PlaylistDeviceGroup { PlaylistId = playlist2.Id, Playlist = playlist2, DeviceGroupId = _deviceGroup.Id, DeviceGroup = _deviceGroup, Play = true });
        _dbContext.SaveChanges();

        SetDeviceContext(_device.Id);

        var result = await _controller.DownloadPlaylist();

        Assert.That(result, Is.TypeOf<FileContentResult>());
        var fileResult = (FileContentResult)result;
        var content = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);
        
        Assert.That(content, Does.Contain("0001/video10.mp4"));
        Assert.That(content, Does.Not.Contain("0001/video9.mp4"));
    }

    [Test]
    public async Task DownloadPlaylist_PlaylistWithPlayTrueButNoVideos_ReturnsEmptyM3u()
    {
        var playlist = new Playlist { Id = 10, Title = "Empty Playlist", Filename = "empty.json", AccountId = _account.Id, Account = _account };
        _dbContext.Playlists.Add(playlist);
        _dbContext.PlaylistDeviceGroups.Add(new PlaylistDeviceGroup 
        { 
            PlaylistId = playlist.Id, 
            Playlist = playlist, 
            DeviceGroupId = _deviceGroup.Id, 
            DeviceGroup = _deviceGroup,
            Play = true 
        });
        _dbContext.SaveChanges();

        SetDeviceContext(_device.Id);

        var result = await _controller.DownloadPlaylist();

        Assert.That(result, Is.TypeOf<FileContentResult>());
        var fileResult = (FileContentResult)result;
        var content = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);

        Assert.That(content, Does.StartWith("#EXTM3U"));
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines.Length, Is.EqualTo(1)); // Only #EXTM3U line
    }

    #region SHA256 On-The-Fly Calculation Tests

    [Test]
    public async Task GetManifest_MissingSha256_CalculatesOnTheFly_AndSavesToDatabase()
    {
        var playlist = new Playlist { Id = 20, Title = "Playlist 20", Filename = "playlist20.json", AccountId = _account.Id, Account = _account };
        var video = new Video
        {
            Id = 100,
            Title = "Video 100",
            Filename = "0001/video100.mp4",
            OriginalFilename = "video100.mp4",
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

        // Setup video storage service to return a valid file path
        var tempFile = System.IO.Path.GetTempFileName();
        System.IO.File.WriteAllText(tempFile, "test video content");
        try
        {
            _mockVideoStorageService.Setup(s => s.GetAbsolutePath(video.Filename)).Returns(tempFile);

            SetDeviceContext(_device.Id);

            var result = await _controller.GetManifest();

            // Verify manifest was returned successfully
            Assert.That(result.Value, Is.Not.Null);
            var manifest = result.Value!.ToList();
            Assert.That(manifest, Has.Count.EqualTo(1));
            Assert.That(manifest[0].Id, Is.EqualTo(video.Id));
            Assert.That(manifest[0].Sha256, Is.Not.Null.And.Not.Empty, "SHA256 should be calculated on-the-fly");
            Assert.That(manifest[0].Sha256, Has.Length.EqualTo(64), "SHA256 should be 64 hex characters");

            // Verify SHA256 was saved to database
            var savedVideo = await _dbContext.Videos.FindAsync(video.Id);
            Assert.That(savedVideo, Is.Not.Null);
            Assert.That(savedVideo!.Sha256, Is.EqualTo(manifest[0].Sha256), "SHA256 should be persisted to database");
        }
        finally
        {
            if (System.IO.File.Exists(tempFile))
            {
                System.IO.File.Delete(tempFile);
            }
        }
    }

    [Test]
    public async Task GetManifest_MultipleMissingSha256_CalculatesAllAndSavesToDatabase()
    {
        var playlist = new Playlist { Id = 21, Title = "Playlist 21", Filename = "playlist21.json", AccountId = _account.Id, Account = _account };
        var video1 = new Video
        {
            Id = 101,
            Title = "Video 101",
            Filename = "0001/video101.mp4",
            OriginalFilename = "video101.mp4",
            FileSizeBytes = 1024,
            AccountId = _account.Id,
            Account = _account,
            Sha256 = null
        };
        var video2 = new Video
        {
            Id = 102,
            Title = "Video 102",
            Filename = "0001/video102.mp4",
            OriginalFilename = "video102.mp4",
            FileSizeBytes = 2048,
            AccountId = _account.Id,
            Account = _account,
            Sha256 = null
        };

        _dbContext.Playlists.Add(playlist);
        _dbContext.Videos.AddRange(video1, video2);
        _dbContext.VideoPlaylists.AddRange(
            new VideoPlaylist { VideoId = video1.Id, Video = video1, PlaylistId = playlist.Id, Playlist = playlist },
            new VideoPlaylist { VideoId = video2.Id, Video = video2, PlaylistId = playlist.Id, Playlist = playlist });
        _dbContext.PlaylistDeviceGroups.Add(new PlaylistDeviceGroup { PlaylistId = playlist.Id, Playlist = playlist, DeviceGroupId = _deviceGroup.Id, DeviceGroup = _deviceGroup });
        _dbContext.SaveChanges();

        // Setup video storage service for both files
        var tempFile1 = System.IO.Path.GetTempFileName();
        var tempFile2 = System.IO.Path.GetTempFileName();
        System.IO.File.WriteAllText(tempFile1, "content1");
        System.IO.File.WriteAllText(tempFile2, "content2");
        try
        {
            _mockVideoStorageService.Setup(s => s.GetAbsolutePath(video1.Filename)).Returns(tempFile1);
            _mockVideoStorageService.Setup(s => s.GetAbsolutePath(video2.Filename)).Returns(tempFile2);

            SetDeviceContext(_device.Id);

            var result = await _controller.GetManifest();

            // Verify both manifests have SHA256
            Assert.That(result.Value, Is.Not.Null);
            var manifest = result.Value!.ToList();
            Assert.That(manifest, Has.Count.EqualTo(2));

            foreach (var item in manifest)
            {
                Assert.That(item.Sha256, Is.Not.Null.And.Not.Empty);
                Assert.That(item.Sha256, Has.Length.EqualTo(64));
            }

            // Verify both were saved
            var saved1 = await _dbContext.Videos.FindAsync(video1.Id);
            var saved2 = await _dbContext.Videos.FindAsync(video2.Id);
            Assert.That(saved1!.Sha256, Is.Not.Null);
            Assert.That(saved2!.Sha256, Is.Not.Null);
            Assert.That(saved1.Sha256, Is.Not.EqualTo(saved2.Sha256), "Different files should have different SHA256");
        }
        finally
        {
            if (System.IO.File.Exists(tempFile1))
                System.IO.File.Delete(tempFile1);
            if (System.IO.File.Exists(tempFile2))
                System.IO.File.Delete(tempFile2);
        }
    }

    [Test]
    public async Task GetManifest_FileNotFoundForSha256Calc_Returns500()
    {
        var playlist = new Playlist { Id = 22, Title = "Playlist 22", Filename = "playlist22.json", AccountId = _account.Id, Account = _account };
        var video = new Video
        {
            Id = 103,
            Title = "Video 103",
            Filename = "0001/nonexistent.mp4",
            OriginalFilename = "nonexistent.mp4",
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

        // Setup to return a path that doesn't exist
        _mockVideoStorageService.Setup(s => s.GetAbsolutePath(video.Filename)).Returns("/nonexistent/path/video.mp4");

        SetDeviceContext(_device.Id);

        var result = await _controller.GetManifest();

        // Should return 500 when file not found
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result.Result!;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
    }

    [Test]
    public async Task GetManifest_MixedSha256_CalculatesOnlyMissing()
    {
        var playlist = new Playlist { Id = 23, Title = "Playlist 23", Filename = "playlist23.json", AccountId = _account.Id, Account = _account };
        var video1 = new Video
        {
            Id = 104,
            Title = "Video 104",
            Filename = "0001/video104.mp4",
            OriginalFilename = "video104.mp4",
            FileSizeBytes = 1024,
            AccountId = _account.Id,
            Account = _account,
            Sha256 = "abc123def456abc123def456abc123def456abc123def456abc123def456abc1" // Existing SHA256
        };
        var video2 = new Video
        {
            Id = 105,
            Title = "Video 105",
            Filename = "0001/video105.mp4",
            OriginalFilename = "video105.mp4",
            FileSizeBytes = 2048,
            AccountId = _account.Id,
            Account = _account,
            Sha256 = null // Missing SHA256
        };

        _dbContext.Playlists.Add(playlist);
        _dbContext.Videos.AddRange(video1, video2);
        _dbContext.VideoPlaylists.AddRange(
            new VideoPlaylist { VideoId = video1.Id, Video = video1, PlaylistId = playlist.Id, Playlist = playlist },
            new VideoPlaylist { VideoId = video2.Id, Video = video2, PlaylistId = playlist.Id, Playlist = playlist });
        _dbContext.PlaylistDeviceGroups.Add(new PlaylistDeviceGroup { PlaylistId = playlist.Id, Playlist = playlist, DeviceGroupId = _deviceGroup.Id, DeviceGroup = _deviceGroup });
        _dbContext.SaveChanges();

        var tempFile = System.IO.Path.GetTempFileName();
        System.IO.File.WriteAllText(tempFile, "new content");
        try
        {
            _mockVideoStorageService.Setup(s => s.GetAbsolutePath(video2.Filename)).Returns(tempFile);

            SetDeviceContext(_device.Id);

            var result = await _controller.GetManifest();

            Assert.That(result.Value, Is.Not.Null);
            var manifest = result.Value!.ToList();
            Assert.That(manifest, Has.Count.EqualTo(2));

            var video1Item = manifest.Single(m => m.Id == video1.Id);
            var video2Item = manifest.Single(m => m.Id == video2.Id);

            // Video1 should keep existing SHA256
            Assert.That(video1Item.Sha256, Is.EqualTo(video1.Sha256));

            // Video2 should have calculated SHA256
            Assert.That(video2Item.Sha256, Is.Not.EqualTo(video1Item.Sha256));
            Assert.That(video2Item.Sha256, Is.Not.Null.And.Not.Empty);
        }
        finally
        {
            if (System.IO.File.Exists(tempFile))
                System.IO.File.Delete(tempFile);
        }
    }

    #endregion
}

