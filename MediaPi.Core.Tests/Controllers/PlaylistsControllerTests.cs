// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;
using NUnit.Framework;

using MediaPi.Core.Controllers;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services;

namespace MediaPi.Core.Tests.Controllers;

[TestFixture]
public class PlaylistsControllerTests
{
#pragma warning disable CS8618
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private Mock<ILogger<PlaylistsController>> _mockLogger;
    private AppDbContext _dbContext;
    private PlaylistsController _controller;
    private User _admin;
    private User _managerAccount1;
    private User _managerAccount2;
    private Role _adminRole;
    private Role _managerRole;
    private Account _account1;
    private Account _account2;
    private Video _video1Acc1;
    private Video _video2Acc1;
    private Video _videoAcc2;
    private Playlist _playlist1;
    private Playlist _playlist2;
    private UserInformationService _userInformationService;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"playlists_controller_test_db_{Guid.NewGuid()}")
            .Options;

        _dbContext = new AppDbContext(options);

        _adminRole = new Role { RoleId = UserRoleConstants.SystemAdministrator, Name = "Admin" };
        _managerRole = new Role { RoleId = UserRoleConstants.AccountManager, Name = "Manager" };
        _dbContext.Roles.AddRange(_adminRole, _managerRole);

        _account1 = new Account { Id = 1, Name = "Account 1" };
        _account2 = new Account { Id = 2, Name = "Account 2" };
        _dbContext.Accounts.AddRange(_account1, _account2);

        _video1Acc1 = new Video { Id = 1, Title = "Video1", Filename = "v1.mp4", AccountId = _account1.Id, Account = _account1 };
        _video2Acc1 = new Video { Id = 2, Title = "Video2", Filename = "v2.mp4", AccountId = _account1.Id, Account = _account1 };
        _videoAcc2 = new Video { Id = 3, Title = "Video3", Filename = "v3.mp4", AccountId = _account2.Id, Account = _account2 };
        _dbContext.Videos.AddRange(_video1Acc1, _video2Acc1, _videoAcc2);

        _playlist1 = new Playlist
        {
            Id = 1,
            Title = "Playlist 1",
            Filename = "p1.json",
            AccountId = _account1.Id,
            Account = _account1,
            VideoPlaylists =
            [
                new VideoPlaylist { VideoId = _video1Acc1.Id, PlaylistId = 1 },
                new VideoPlaylist { VideoId = _video2Acc1.Id, PlaylistId = 1 }
            ]
        };

        _playlist2 = new Playlist
        {
            Id = 2,
            Title = "Playlist 2",
            Filename = "p2.json",
            AccountId = _account2.Id,
            Account = _account2,
            VideoPlaylists =
            [
                new VideoPlaylist { VideoId = _videoAcc2.Id, PlaylistId = 2 }
            ]
        };

        _dbContext.Playlists.AddRange(_playlist1, _playlist2);

        string pass = BCrypt.Net.BCrypt.HashPassword("pwd");

        _admin = new User
        {
            Id = 1,
            Email = "admin@example.com",
            Password = pass,
            UserRoles = [ new UserRole { UserId = 1, RoleId = _adminRole.Id, Role = _adminRole } ]
        };

        _managerAccount1 = new User
        {
            Id = 2,
            Email = "manager1@example.com",
            Password = pass,
            UserRoles = [ new UserRole { UserId = 2, RoleId = _managerRole.Id, Role = _managerRole } ],
            UserAccounts = [ new UserAccount { UserId = 2, AccountId = _account1.Id, Account = _account1 } ]
        };

        _managerAccount2 = new User
        {
            Id = 3,
            Email = "manager2@example.com",
            Password = pass,
            UserRoles = [ new UserRole { UserId = 3, RoleId = _managerRole.Id, Role = _managerRole } ],
            UserAccounts = [ new UserAccount { UserId = 3, AccountId = _account2.Id, Account = _account2 } ]
        };

        _dbContext.Users.AddRange(_admin, _managerAccount1, _managerAccount2);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockLogger = new Mock<ILogger<PlaylistsController>>();
        _userInformationService = new UserInformationService(_dbContext);
    }

    private void SetCurrentUser(int? id)
    {
        var context = new DefaultHttpContext();
        if (id.HasValue) context.Items["UserId"] = id.Value;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);
        _controller = new PlaylistsController(
            _mockHttpContextAccessor.Object,
            _userInformationService,
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
    public async Task GetPlaylists_Admin_ReturnsAll()
    {
        SetCurrentUser(_admin.Id);
        var result = await _controller.GetPlaylists();
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task GetPlaylists_Manager_ReturnsOwn()
    {
        SetCurrentUser(_managerAccount1.Id);
        var result = await _controller.GetPlaylists();
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(1));
        Assert.That(result.Value!.First().Id, Is.EqualTo(_playlist1.Id));
    }

    [Test]
    public async Task GetPlaylistsByAccount_Admin_SpecificAccount()
    {
        SetCurrentUser(_admin.Id);
        var result = await _controller.GetPlaylistsByAccount(_account1.Id);
        Assert.That(result.Value, Is.Not.Null);
        var list = result.Value!.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Id, Is.EqualTo(_playlist1.Id));
    }

    [Test]
    public async Task GetPlaylistsByAccount_Manager_OwnAccount()
    {
        SetCurrentUser(_managerAccount1.Id);
        var result = await _controller.GetPlaylistsByAccount(_account1.Id);
        Assert.That(result.Value, Is.Not.Null);
        var list = result.Value!.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Id, Is.EqualTo(_playlist1.Id));
    }

    [Test]
    public async Task GetPlaylistsByAccount_Manager_OtherAccount_Forbidden()
    {
        SetCurrentUser(_managerAccount1.Id);
        var result = await _controller.GetPlaylistsByAccount(_account2.Id);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result.Result!;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task CreatePlaylist_ManagerWrongAccount_Returns403()
    {
        SetCurrentUser(_managerAccount1.Id);
        var item = new PlaylistCreateItem
        {
            Title = "New",
            Filename = "new.json",
            AccountId = _account2.Id,
            VideoIds = [_videoAcc2.Id]
        };

        var result = await _controller.CreatePlaylist(item);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result.Result!;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task CreatePlaylist_VideoFromOtherAccount_Returns400()
    {
        SetCurrentUser(_admin.Id);
        var item = new PlaylistCreateItem
        {
            Title = "New",
            Filename = "new.json",
            AccountId = _account1.Id,
            VideoIds = [_videoAcc2.Id]
        };

        var result = await _controller.CreatePlaylist(item);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result.Result!;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task UpdatePlaylist_InvalidVideo_Returns404()
    {
        SetCurrentUser(_admin.Id);
        var item = new PlaylistUpdateItem
        {
            Title = "Updated",
            Filename = "updated.json",
            VideoIds = [999]
        };

        var result = await _controller.UpdatePlaylist(_playlist1.Id, item);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task UpdatePlaylist_Manager_UpdatesVideos()
    {
        SetCurrentUser(_managerAccount1.Id);
        var item = new PlaylistUpdateItem
        {
            Title = "Updated",
            Filename = "updated.json",
            VideoIds = [_video1Acc1.Id]
        };

        var result = await _controller.UpdatePlaylist(_playlist1.Id, item);
        Assert.That(result, Is.TypeOf<NoContentResult>());

        var playlist = await _dbContext.Playlists
            .Include(p => p.VideoPlaylists)
            .FirstAsync(p => p.Id == _playlist1.Id);
        Assert.That(playlist.Title, Is.EqualTo("Updated"));
        Assert.That(playlist.VideoPlaylists.Select(vp => vp.VideoId), Is.EquivalentTo(new[] { _video1Acc1.Id }));
    }

    [Test]
    public async Task DeletePlaylist_Admin_RemovesPlaylist()
    {
        SetCurrentUser(_admin.Id);
        var result = await _controller.DeletePlaylist(_playlist1.Id);
        Assert.That(result, Is.TypeOf<NoContentResult>());
        var playlist = await _dbContext.Playlists.FindAsync(_playlist1.Id);
        Assert.That(playlist, Is.Null);
    }
}
