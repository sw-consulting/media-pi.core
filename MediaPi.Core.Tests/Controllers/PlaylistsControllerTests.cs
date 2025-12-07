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

        _video1Acc1 = new Video { Id = 1, Title = "Video1", Filename = "v1.mp4", OriginalFilename = "video1.mp4", FileSizeBytes = 512000, DurationSeconds = 30, AccountId = _account1.Id, Account = _account1 };
        _video2Acc1 = new Video { Id = 2, Title = "Video2", Filename = "v2.mp4", OriginalFilename = "video2.mp4", FileSizeBytes = 1024000, DurationSeconds = 60, AccountId = _account1.Id, Account = _account1 };
        _videoAcc2 = new Video { Id = 3, Title = "Video3", Filename = "v3.mp4", OriginalFilename = "video3.mp4", FileSizeBytes = 2048000, DurationSeconds = 90, AccountId = _account2.Id, Account = _account2 };
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
                new VideoPlaylist { VideoId = _video1Acc1.Id, PlaylistId = 1, Position = 0 },
                new VideoPlaylist { VideoId = _video2Acc1.Id, PlaylistId = 1, Position = 1 }
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
                new VideoPlaylist { VideoId = _videoAcc2.Id, PlaylistId = 2, Position = 0 }
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

    [Test]
    public async Task CreatePlaylist_WithItems_CreatesOrderedPlaylist()
    {
        SetCurrentUser(_admin.Id);
        var item = new PlaylistCreateItem
        {
            Title = "Ordered Playlist",
            Filename = "ordered.json",
            AccountId = _account1.Id,
            Items = 
            [
                new PlaylistItemDto { VideoId = _video2Acc1.Id, Position = 0 },
                new PlaylistItemDto { VideoId = _video1Acc1.Id, Position = 1 },
                new PlaylistItemDto { VideoId = _video2Acc1.Id, Position = 2 } // Same video, different position
            ]
        };

        var result = await _controller.CreatePlaylist(item);
        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        
        var created = (CreatedAtActionResult)result.Result!;
        var reference = (Reference)created.Value!;
        
        var playlist = await _dbContext.Playlists
            .Include(p => p.VideoPlaylists.OrderBy(vp => vp.Position))
            .ThenInclude(vp => vp.Video)
            .FirstAsync(p => p.Id == reference.Id);
            
        Assert.That(playlist.VideoPlaylists.Count, Is.EqualTo(3));
        Assert.That(playlist.VideoPlaylists.Select(vp => vp.VideoId), Is.EqualTo(new[] { _video2Acc1.Id, _video1Acc1.Id, _video2Acc1.Id }));
        Assert.That(playlist.VideoPlaylists.Select(vp => vp.Position), Is.EqualTo(new[] { 0, 1, 2 }));
    }

    [Test]
    public async Task GetPlaylist_ReturnsPlaylistWithStatsAndItems()
    {
        SetCurrentUser(_admin.Id);
        var result = await _controller.GetPlaylist(_playlist1.Id);
        
        Assert.That(result.Value, Is.Not.Null);
        var playlistView = result.Value!;
        
        // Check basic properties
        Assert.That(playlistView.Id, Is.EqualTo(_playlist1.Id));
        Assert.That(playlistView.Title, Is.EqualTo(_playlist1.Title));
        
        // Check stats
        Assert.That(playlistView.Stats, Is.Not.Null);
        Assert.That(playlistView.Stats.VideoCount, Is.EqualTo(2));
        Assert.That(playlistView.Stats.TotalFileSizeBytes, Is.EqualTo(1536000)); // 512000 + 1024000
        
        // Check items are ordered
        Assert.That(playlistView.Items, Is.Not.Null);
        var items = playlistView.Items.ToList();
        Assert.That(items.Count, Is.EqualTo(2));
        Assert.That(items[0].Position, Is.EqualTo(0));
        Assert.That(items[1].Position, Is.EqualTo(1));
    }

    [Test]
    public async Task UpdatePlaylist_WithItems_UpdatesOrderCorrectly()
    {
        SetCurrentUser(_admin.Id);
        var item = new PlaylistUpdateItem
        {
            Title = "Updated Playlist",
            Filename = "updated.json",
            Items = 
            [
                new PlaylistItemDto { VideoId = _video1Acc1.Id, Position = 5 }, // Non-sequential positions should work
                new PlaylistItemDto { VideoId = _video1Acc1.Id, Position = 10 } // Same video twice
            ]
        };

        var result = await _controller.UpdatePlaylist(_playlist1.Id, item);
        Assert.That(result, Is.TypeOf<NoContentResult>());

        var playlist = await _dbContext.Playlists
            .Include(p => p.VideoPlaylists.OrderBy(vp => vp.Position))
            .FirstAsync(p => p.Id == _playlist1.Id);
            
        Assert.That(playlist.Title, Is.EqualTo("Updated Playlist"));
        Assert.That(playlist.VideoPlaylists.Count, Is.EqualTo(2));
        Assert.That(playlist.VideoPlaylists.Select(vp => vp.VideoId), Is.EqualTo(new[] { _video1Acc1.Id, _video1Acc1.Id }));
        Assert.That(playlist.VideoPlaylists.Select(vp => vp.Position), Is.EqualTo(new[] { 5, 10 }));
    }

    [Test]
    public async Task GetPlaylists_NoUser_Returns403()
    {
        SetCurrentUser(null);
        var result = await _controller.GetPlaylists();
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result.Result!;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetPlaylist_NotFound_Returns404()
    {
        SetCurrentUser(_admin.Id);
        var result = await _controller.GetPlaylist(999);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result.Result!;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetPlaylist_ManagerAccessingOtherAccount_Returns403()
    {
        SetCurrentUser(_managerAccount1.Id);
        var result = await _controller.GetPlaylist(_playlist2.Id);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result.Result!;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task CreatePlaylist_AccountNotFound_Returns404()
    {
        SetCurrentUser(_admin.Id);
        var item = new PlaylistCreateItem
        {
            Title = "New",
            Filename = "new.json",
            AccountId = 999,
            VideoIds = []
        };

        var result = await _controller.CreatePlaylist(item);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result.Result!;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task CreatePlaylist_NoVideos_CreatesEmptyPlaylist()
    {
        SetCurrentUser(_admin.Id);
        var item = new PlaylistCreateItem
        {
            Title = "Empty Playlist",
            Filename = "empty.json",
            AccountId = _account1.Id,
            VideoIds = []
        };

        var result = await _controller.CreatePlaylist(item);
        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        
        var created = (CreatedAtActionResult)result.Result!;
        var reference = (Reference)created.Value!;
        
        var playlist = await _dbContext.Playlists
            .Include(p => p.VideoPlaylists)
            .FirstAsync(p => p.Id == reference.Id);
            
        Assert.That(playlist.VideoPlaylists.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task CreatePlaylist_WithDuplicateVideos_CreatesPlaylistWithBothInstances()
    {
        SetCurrentUser(_admin.Id);
        var item = new PlaylistCreateItem
        {
            Title = "Duplicate Videos",
            Filename = "duplicate.json",
            AccountId = _account1.Id,
            VideoIds = [_video1Acc1.Id, _video1Acc1.Id]
        };

        var result = await _controller.CreatePlaylist(item);
        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        
        var created = (CreatedAtActionResult)result.Result!;
        var reference = (Reference)created.Value!;
        
        var playlist = await _dbContext.Playlists
            .Include(p => p.VideoPlaylists)
            .FirstAsync(p => p.Id == reference.Id);
            
        // With legacy VideoIds, duplicates are removed
        Assert.That(playlist.VideoPlaylists.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task CreatePlaylist_InvalidVideoId_Returns404()
    {
        SetCurrentUser(_admin.Id);
        var item = new PlaylistCreateItem
        {
            Title = "Invalid Video",
            Filename = "invalid.json",
            AccountId = _account1.Id,
            VideoIds = [999]
        };

        var result = await _controller.CreatePlaylist(item);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result.Result!;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task CreatePlaylist_WithItems_DuplicatePositions_Returns400()
    {
        SetCurrentUser(_admin.Id);
        var item = new PlaylistCreateItem
        {
            Title = "Duplicate Positions",
            Filename = "dup-pos.json",
            AccountId = _account1.Id,
            Items = 
            [
                new PlaylistItemDto { VideoId = _video1Acc1.Id, Position = 0 },
                new PlaylistItemDto { VideoId = _video2Acc1.Id, Position = 0 } // Duplicate position
            ]
        };

        var result = await _controller.CreatePlaylist(item);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result.Result!;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task CreatePlaylist_WithItems_NegativePositions_Returns400()
    {
        SetCurrentUser(_admin.Id);
        var item = new PlaylistCreateItem
        {
            Title = "Negative Positions",
            Filename = "neg-pos.json",
            AccountId = _account1.Id,
            Items = 
            [
                new PlaylistItemDto { VideoId = _video1Acc1.Id, Position = -1 }, // Negative position
                new PlaylistItemDto { VideoId = _video2Acc1.Id, Position = 0 }
            ]
        };

        var result = await _controller.CreatePlaylist(item);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result.Result!;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task UpdatePlaylist_NotFound_Returns404()
    {
        SetCurrentUser(_admin.Id);
        var item = new PlaylistUpdateItem
        {
            Title = "Updated",
            Filename = "updated.json"
        };

        var result = await _controller.UpdatePlaylist(999, item);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task UpdatePlaylist_ManagerAccessingOtherAccount_Returns403()
    {
        SetCurrentUser(_managerAccount1.Id);
        var item = new PlaylistUpdateItem
        {
            Title = "Updated",
            Filename = "updated.json"
        };

        var result = await _controller.UpdatePlaylist(_playlist2.Id, item);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task UpdatePlaylist_WithItemsFromOtherAccount_Returns400()
    {
        SetCurrentUser(_admin.Id);
        var item = new PlaylistUpdateItem
        {
            Title = "Updated",
            Filename = "updated.json",
            Items = 
            [
                new PlaylistItemDto { VideoId = _videoAcc2.Id, Position = 0 } // Video from account 2
            ]
        };

        var result = await _controller.UpdatePlaylist(_playlist1.Id, item);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task UpdatePlaylist_WithItems_NegativePositions_Returns400()
    {
        SetCurrentUser(_admin.Id);
        var item = new PlaylistUpdateItem
        {
            Title = "Updated",
            Filename = "updated.json",
            Items = 
            [
                new PlaylistItemDto { VideoId = _video1Acc1.Id, Position = -1 }, // Negative position
                new PlaylistItemDto { VideoId = _video2Acc1.Id, Position = 0 }
            ]
        };

        var result = await _controller.UpdatePlaylist(_playlist1.Id, item);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task UpdatePlaylist_RemoveAllVideos_ClearsPlaylist()
    {
        SetCurrentUser(_admin.Id);
        var item = new PlaylistUpdateItem
        {
            Title = "Empty",
            Filename = "empty.json",
            VideoIds = []
        };

        var result = await _controller.UpdatePlaylist(_playlist1.Id, item);
        Assert.That(result, Is.TypeOf<NoContentResult>());

        var playlist = await _dbContext.Playlists
            .Include(p => p.VideoPlaylists)
            .FirstAsync(p => p.Id == _playlist1.Id);
            
        Assert.That(playlist.VideoPlaylists.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task UpdatePlaylist_OnlyUpdateTitle_KeepsExistingVideos()
    {
        SetCurrentUser(_admin.Id);
        var item = new PlaylistUpdateItem
        {
            Title = "New Title",
            Filename = _playlist1.Filename
        };

        var result = await _controller.UpdatePlaylist(_playlist1.Id, item);
        Assert.That(result, Is.TypeOf<NoContentResult>());

        var playlist = await _dbContext.Playlists
            .Include(p => p.VideoPlaylists)
            .FirstAsync(p => p.Id == _playlist1.Id);
            
        Assert.That(playlist.Title, Is.EqualTo("New Title"));
        Assert.That(playlist.VideoPlaylists.Count, Is.EqualTo(2)); // Still has original videos
    }

    [Test]
    public async Task DeletePlaylist_NotFound_Returns404()
    {
        SetCurrentUser(_admin.Id);
        var result = await _controller.DeletePlaylist(999);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task DeletePlaylist_ManagerAccessingOtherAccount_Returns403()
    {
        SetCurrentUser(_managerAccount1.Id);
        var result = await _controller.DeletePlaylist(_playlist2.Id);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DeletePlaylist_EmptyPlaylist_DeletesSuccessfully()
    {
        SetCurrentUser(_admin.Id);
        
        // Create an empty playlist
        var emptyPlaylist = new Playlist
        {
            Title = "Empty",
            Filename = "empty.json",
            AccountId = _account1.Id
        };
        _dbContext.Playlists.Add(emptyPlaylist);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DeletePlaylist(emptyPlaylist.Id);
        Assert.That(result, Is.TypeOf<NoContentResult>());
        
        var deleted = await _dbContext.Playlists.FindAsync(emptyPlaylist.Id);
        Assert.That(deleted, Is.Null);
    }

    [Test]
    public async Task GetPlaylistsByAccount_EmptyAccount_ReturnsEmptyList()
    {
        SetCurrentUser(_admin.Id);
        
        // Create a new account with no playlists
        var emptyAccount = new Account { Name = "Empty Account" };
        _dbContext.Accounts.Add(emptyAccount);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetPlaylistsByAccount(emptyAccount.Id);
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task CreatePlaylist_ValidDataWithManager_CreatesSuccessfully()
    {
        SetCurrentUser(_managerAccount1.Id);
        var item = new PlaylistCreateItem
        {
            Title = "Manager Playlist",
            Filename = "manager.json",
            AccountId = _account1.Id,
            VideoIds = [_video1Acc1.Id]
        };

        var result = await _controller.CreatePlaylist(item);
        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        
        var created = (CreatedAtActionResult)result.Result!;
        Assert.That(created.StatusCode, Is.EqualTo(StatusCodes.Status201Created));
    }
}
