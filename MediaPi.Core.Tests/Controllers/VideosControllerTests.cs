// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MediaPi.Core.Controllers;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services;
using MediaPi.Core.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Controllers;

[TestFixture]
public class VideosControllerTests
{
#pragma warning disable CS8618
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private Mock<ILogger<VideosController>> _mockLogger;
    private Mock<IVideoStorageService> _mockVideoStorageService;
    private AppDbContext _dbContext;
    private VideosController _controller;
    private UserInformationService _userInformationService;

    private User _admin;
    private User _managerAccount1;
    private User _managerAccount2;
    private Role _adminRole;
    private Role _managerRole;
    private Account _account1;
    private Account _account2;
    private Playlist _playlistAccount1;
    private Playlist _playlistAccount1Second;
    private Playlist _playlistAccount2;
    private Video _videoAccount1;
    private Video _videoAccount2;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"videos_controller_test_db_{Guid.NewGuid()}")
            .Options;

        _dbContext = new AppDbContext(options);

        _adminRole = new Role { RoleId = UserRoleConstants.SystemAdministrator, Name = "Admin" };
        _managerRole = new Role { RoleId = UserRoleConstants.AccountManager, Name = "Manager" };
        _dbContext.Roles.AddRange(_adminRole, _managerRole);

        _account1 = new Account { Id = 1, Name = "Account 1" };
        _account2 = new Account { Id = 2, Name = "Account 2" };
        _dbContext.Accounts.AddRange(_account1, _account2);

        _playlistAccount1 = new Playlist { Id = 1, Title = "Playlist 1", Filename = "playlist1.json", AccountId = _account1.Id, Account = _account1 };
        _playlistAccount1Second = new Playlist { Id = 2, Title = "Playlist 2", Filename = "playlist2.json", AccountId = _account1.Id, Account = _account1 };
        _playlistAccount2 = new Playlist { Id = 3, Title = "Playlist 3", Filename = "playlist3.json", AccountId = _account2.Id, Account = _account2 };
        _dbContext.Playlists.AddRange(_playlistAccount1, _playlistAccount1Second, _playlistAccount2);

        _videoAccount1 = new Video { Id = 1, Title = "Video 1", Filename = "0001/video1.mp4", OriginalFilename = "video1.mp4", FileSizeBytes = 1024000, AccountId = _account1.Id, Account = _account1 };
        _videoAccount2 = new Video { Id = 2, Title = "Video 2", Filename = "0001/video2.mp4", OriginalFilename = "video2.mp4", FileSizeBytes = 2048000, AccountId = _account2.Id, Account = _account2 };
        // Unassigned common video available to everyone but managed by administrators only
        var videoUnassigned = new Video { Id = 3, Title = "Public Video", Filename = "0001/public.mp4", OriginalFilename = "public.mp4", FileSizeBytes = 512000, AccountId = null };
        _dbContext.Videos.AddRange(_videoAccount1, _videoAccount2, videoUnassigned);

        const string pass = "pwd";
        string hashed = BCrypt.Net.BCrypt.HashPassword(pass);

        _admin = new User
        {
            Id = 1,
            Email = "admin@example.com",
            Password = hashed,
            UserRoles = [new UserRole { UserId = 1, RoleId = _adminRole.Id, Role = _adminRole }]
        };

        _managerAccount1 = new User
        {
            Id = 2,
            Email = "manager1@example.com",
            Password = hashed,
            UserRoles = [new UserRole { UserId = 2, RoleId = _managerRole.Id, Role = _managerRole }],
            UserAccounts = [new UserAccount { UserId = 2, AccountId = _account1.Id, Account = _account1 }]
        };

        _managerAccount2 = new User
        {
            Id = 3,
            Email = "manager2@example.com",
            Password = hashed,
            UserRoles = [new UserRole { UserId = 3, RoleId = _managerRole.Id, Role = _managerRole }],
            UserAccounts = [new UserAccount { UserId = 3, AccountId = _account2.Id, Account = _account2 }]
        };

        _dbContext.Users.AddRange(_admin, _managerAccount1, _managerAccount2);
        _dbContext.VideoPlaylists.Add(new VideoPlaylist { VideoId = _videoAccount1.Id, PlaylistId = _playlistAccount1.Id, Playlist = _playlistAccount1, Video = _videoAccount1 });
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockLogger = new Mock<ILogger<VideosController>>();
        _mockVideoStorageService = new Mock<IVideoStorageService>();
        _userInformationService = new UserInformationService(_dbContext);
    }

    private void SetCurrentUser(int? id)
    {
        var context = new DefaultHttpContext();
        if (id.HasValue)
        {
            context.Items["UserId"] = id.Value;
        }

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);
        _controller = new VideosController(
            _mockHttpContextAccessor.Object,
            _userInformationService,
            _mockVideoStorageService.Object,
            _dbContext,
            _mockLogger.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Test]
    public async Task GetVideos_Admin_ReturnsAll()
    {
        SetCurrentUser(_admin.Id);
        var result = await _controller.GetVideos();
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(3));
    }

    [Test]
    public async Task GetVideos_Manager_ReturnsOwn()
    {
        SetCurrentUser(_managerAccount1.Id);
        var result = await _controller.GetVideos();
        Assert.That(result.Value, Is.Not.Null);
        // Manager should see videos from own account and unassigned (common) videos
        Assert.That(result.Value!.Count(), Is.EqualTo(2));
        var ids = result.Value!.Select(v => v.Id).ToList();
        Assert.That(ids, Does.Contain(_videoAccount1.Id));
    }

    [Test]
    public async Task GetVideo_ManagerOtherAccount_Returns403()
    {
        SetCurrentUser(_managerAccount1.Id);
        var result = await _controller.GetVideo(_videoAccount2.Id);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result.Result!;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetVideo_Manager_Unassigned_ReturnsOk()
    {
        SetCurrentUser(_managerAccount1.Id);
        // Unassigned video has id 3 per setup
        var result = await _controller.GetVideo(3);
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Id, Is.EqualTo(3));
    }

    [Test]
    public async Task GetVideosByAccount_Admin_Zero_ReturnsUnassigned()
    {
        SetCurrentUser(_admin.Id);
        var result = await _controller.GetVideosByAccount(0);
        Assert.That(result.Value, Is.Not.Null);
        var list = result.Value!.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Id, Is.EqualTo(3));
    }

    [Test]
    public async Task GetVideosByAccount_Manager_Zero_ReturnsUnassigned()
    {
        SetCurrentUser(_managerAccount1.Id);
        var result = await _controller.GetVideosByAccount(0);
        Assert.That(result.Value, Is.Not.Null);
        var list = result.Value!.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Id, Is.EqualTo(3));
    }

    [Test]
    public async Task UploadVideo_Admin_SavesVideo()
    {
        SetCurrentUser(_admin.Id);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var file = new FormFile(stream, 0, stream.Length, "file", "sample.mp4");
        var saveResult = new VideoSaveResult
        {
            Filename = "0002/sample.mp4",
            OriginalFilename = "sample.mp4",
            FileSizeBytes = (uint)stream.Length,
            DurationSeconds = 121 // 2 minutes sample (rounded)
        };
        _mockVideoStorageService
            .Setup(s => s.SaveVideoAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(saveResult);

        var item = new VideoUploadItem
        {
            Title = "Sample",
            AccountId = _account1.Id,
            File = file
        };

        var result = await _controller.UploadVideo(item);
        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var created = (CreatedAtActionResult)result.Result!;
        Assert.That(created.Value, Is.TypeOf<Reference>());
        var reference = (Reference)created.Value!;
        Assert.That(reference.Id, Is.GreaterThan(0));
        Assert.That(_dbContext.Videos.Count(), Is.EqualTo(4));
        var video = await _dbContext.Videos.FindAsync(reference.Id);
        Assert.That(video, Is.Not.Null);
        Assert.That(video!.Filename, Is.EqualTo("0002/sample.mp4"));
        Assert.That(video.OriginalFilename, Is.EqualTo("sample.mp4"));
        Assert.That(video.FileSizeBytes, Is.EqualTo((uint)stream.Length));
        Assert.That(video.DurationSeconds, Is.EqualTo(121u));
        _mockVideoStorageService.Verify(s => s.SaveVideoAsync(file, item.Title, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UploadVideo_MissingFile_Returns400()
    {
        SetCurrentUser(_admin.Id);
        var item = new VideoUploadItem
        {
            Title = "Sample",
            AccountId = _account1.Id,
            File = default! // Use default! to suppress CS8625 for non-nullable reference type in test
        };

        var result = await _controller.UploadVideo(item);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result.Result!;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task UpdateVideo_DoesNotChangeFilename()
    {
        SetCurrentUser(_admin.Id);
        var item = new VideoUpdateItem { Title = "Updated" };
        var result = await _controller.UpdateVideo(_videoAccount1.Id, item);
        Assert.That(result, Is.TypeOf<NoContentResult>());
        var video = await _dbContext.Videos.FindAsync(_videoAccount1.Id);
        Assert.That(video, Is.Not.Null);
        Assert.That(video!.Filename, Is.EqualTo("0001/video1.mp4"));
        Assert.That(video.Title, Is.EqualTo("Updated"));
    }

    [Test]
    public async Task UpdateVideo_PlaylistIdsNull_DoesNotChangeAssociations()
    {
        SetCurrentUser(_admin.Id);
        var item = new VideoUpdateItem { Title = "Updated", PlaylistIds = (List<int>?)null };

        var result = await _controller.UpdateVideo(_videoAccount1.Id, item);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        var playlists = await _dbContext.VideoPlaylists
            .Where(vp => vp.VideoId == _videoAccount1.Id)
            .Select(vp => vp.PlaylistId)
            .ToListAsync();
        Assert.That(playlists, Is.EquivalentTo(new[] { _playlistAccount1.Id }));
    }

    [Test]
    public async Task UpdateVideo_PlaylistIdsEmpty_RemovesAssociations()
    {
        SetCurrentUser(_admin.Id);
        var item = new VideoUpdateItem { Title = "Updated", PlaylistIds = [] };

        var result = await _controller.UpdateVideo(_videoAccount1.Id, item);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        Assert.That(_dbContext.VideoPlaylists.Any(vp => vp.VideoId == _videoAccount1.Id), Is.False);
    }

    [Test]
    public async Task UpdateVideo_PlaylistIdsApplied_AddsAndRemovesCorrectly()
    {
        SetCurrentUser(_admin.Id);
        var item = new VideoUpdateItem { Title = "Updated", PlaylistIds = new List<int> { _playlistAccount1.Id, _playlistAccount1Second.Id } };

        var result = await _controller.UpdateVideo(_videoAccount1.Id, item);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        var playlists = await _dbContext.VideoPlaylists
            .Where(vp => vp.VideoId == _videoAccount1.Id)
            .Select(vp => vp.PlaylistId)
            .ToListAsync();
        Assert.That(playlists, Is.EquivalentTo(new[] { _playlistAccount1.Id, _playlistAccount1Second.Id }));
    }

    [Test]
    public async Task UpdateVideo_PlaylistIdsMissingPlaylist_Returns404()
    {
        SetCurrentUser(_admin.Id);
        var item = new VideoUpdateItem { Title = "Updated", PlaylistIds = new List<int> { 999 } };

        var result = await _controller.UpdateVideo(_videoAccount1.Id, item);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task UpdateVideo_PlaylistIdsAccountMismatch_Returns400()
    {
        SetCurrentUser(_admin.Id);
        var item = new VideoUpdateItem { Title = "Updated", PlaylistIds = new List<int> { _playlistAccount2.Id } };

        var result = await _controller.UpdateVideo(_videoAccount1.Id, item);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task DeleteVideo_RemovesVideoAndFile()
    {
        SetCurrentUser(_admin.Id);
        _mockVideoStorageService
            .Setup(s => s.DeleteVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.DeleteVideo(_videoAccount1.Id);
        Assert.That(result, Is.TypeOf<NoContentResult>());
        Assert.That(_dbContext.Videos.Any(v => v.Id == _videoAccount1.Id), Is.False);
        _mockVideoStorageService.Verify(s => s.DeleteVideoAsync(_videoAccount1.Filename, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetVideosByAccount_Admin_SpecificAccount()
    {
        SetCurrentUser(_admin.Id);
        var result = await _controller.GetVideosByAccount(_account1.Id);
        Assert.That(result.Value, Is.Not.Null);
        var list = result.Value!.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Id, Is.EqualTo(_videoAccount1.Id));
    }

    [Test]
    public async Task GetVideosByAccount_Manager_OwnAccount()
    {
        SetCurrentUser(_managerAccount1.Id);
        var result = await _controller.GetVideosByAccount(_account1.Id);
        Assert.That(result.Value, Is.Not.Null);
        var list = result.Value!.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Id, Is.EqualTo(_videoAccount1.Id));
    }

    [Test]
    public async Task GetVideosByAccount_Manager_OtherAccount_Forbidden()
    {
        SetCurrentUser(_managerAccount1.Id);
        var result = await _controller.GetVideosByAccount(_account2.Id);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result.Result!;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task UpdateVideo_Manager_Unassigned_Returns403()
    {
        SetCurrentUser(_managerAccount1.Id);
        var item = new VideoUpdateItem { Title = "Updated" };
        var result = await _controller.UpdateVideo(3, item);
        Assert.That(result, Is.TypeOf<ObjectResult>()); // Forbidden
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DeleteVideo_Manager_Unassigned_Returns403()
    {
        SetCurrentUser(_managerAccount1.Id);
        var result = await _controller.DeleteVideo(3);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task UploadVideo_DuplicateFilename_Returns409AndCleansUpFile()
    {
        SetCurrentUser(_admin.Id);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var file = new FormFile(stream, 0, stream.Length, "file", "duplicate.mp4");
        var saveResult = new VideoSaveResult
        {
            Filename = "0001/video1.mp4", // Same as existing video
            OriginalFilename = "duplicate.mp4",
            FileSizeBytes = (uint)stream.Length,
            DurationSeconds = 121
        };
        _mockVideoStorageService
            .Setup(s => s.SaveVideoAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(saveResult);
        _mockVideoStorageService
            .Setup(s => s.DeleteVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var item = new VideoUploadItem
        {
            Title = "Duplicate Video",
            AccountId = _account1.Id,
            File = file
        };

        var result = await _controller.UploadVideo(item);
        
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result.Result!;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        
        var errMessage = (ErrMessage)obj.Value!;
        Assert.That(errMessage.Msg, Does.Contain("0001/video1.mp4"));
        
        // Verify file cleanup was called
        _mockVideoStorageService.Verify(s => s.DeleteVideoAsync(saveResult.Filename, It.IsAny<CancellationToken>()), Times.Once);
        
        // Verify video count didn't increase
        Assert.That(_dbContext.Videos.Count(), Is.EqualTo(3));
    }

    [Test]
    public async Task UploadVideo_UniqueFilename_SavesSuccessfully()
    {
        SetCurrentUser(_admin.Id);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var file = new FormFile(stream, 0, stream.Length, "file", "unique.mp4");
        var saveResult = new VideoSaveResult
        {
            Filename = "0002/unique.mp4", // Unique filename
            OriginalFilename = "unique.mp4",
            FileSizeBytes = (uint)stream.Length,
            DurationSeconds = 60
        };
        _mockVideoStorageService
            .Setup(s => s.SaveVideoAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(saveResult);

        var item = new VideoUploadItem
        {
            Title = "Unique Video",
            AccountId = _account1.Id,
            File = file
        };

        var result = await _controller.UploadVideo(item);
        
        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var created = (CreatedAtActionResult)result.Result!;
        Assert.That(created.Value, Is.TypeOf<Reference>());
        var reference = (Reference)created.Value!;
        Assert.That(reference.Id, Is.GreaterThan(0));
        
        // Verify new video was added
        Assert.That(_dbContext.Videos.Count(), Is.EqualTo(4));
        var video = await _dbContext.Videos.FindAsync(reference.Id);
        Assert.That(video, Is.Not.Null);
        Assert.That(video!.Filename, Is.EqualTo("0002/unique.mp4"));
        
        // Verify delete was NOT called since there was no conflict
        _mockVideoStorageService.Verify(s => s.DeleteVideoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

}
