// Copyright (c) 2025 sw.consulting

using System;
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
public class PlaylistsControllerEmptyPlaylistTests
{
#pragma warning disable CS8618
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private Mock<ILogger<PlaylistsController>> _mockLogger;
    private AppDbContext _dbContext;
    private PlaylistsController _controller;
    private User _admin;
    private Role _adminRole;
    private Account _account1;
    private UserInformationService _userInformationService;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"playlists_controller_empty_test_db_{Guid.NewGuid()}")
            .Options;

        _dbContext = new AppDbContext(options);

        _adminRole = new Role { RoleId = UserRoleConstants.SystemAdministrator, Name = "Admin" };
        _dbContext.Roles.Add(_adminRole);

        _account1 = new Account { Id = 1, Name = "Account 1" };
        _dbContext.Accounts.Add(_account1);

        _dbContext.SaveChanges();

        _admin = new User
        {
            Id = 1,
            Email = "admin@example.com",
            Password = "pwd",
            UserRoles = [ new UserRole { UserId = 1, RoleId = _adminRole.Id, Role = _adminRole } ]
        };

        _dbContext.Users.Add(_admin);
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
    public async Task CreatePlaylist_EmptyItems_CreatesEmptyPlaylist()
    {
        SetCurrentUser(_admin.Id);

        var item = new PlaylistCreateItem
        {
            Title = "Empty",
            Filename = "empty.json",
            AccountId = _account1.Id,
            Items = []
        };

        var result = await _controller.CreatePlaylist(item);
        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());

        var created = (CreatedAtActionResult)result.Result!;
        var reference = (Reference)created.Value!;

        var playlist = await _dbContext.Playlists.FindAsync(reference.Id);
        Assert.That(playlist, Is.Not.Null);
        Assert.That(playlist!.VideoPlaylists, Is.Not.Null);
        Assert.That(playlist.VideoPlaylists.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task UpdatePlaylist_SetEmptyItems_RemovesAllVideos()
    {
        // Create a playlist with a video
        var video = new Video { Id = 1, Title = "V", Filename = "v.mp4", OriginalFilename = "v.mp4", FileSizeBytes = 100, DurationSeconds = 10, AccountId = _account1.Id };
        _dbContext.Videos.Add(video);

        var playlist = new Playlist { Title = "P", Filename = "p.json", AccountId = _account1.Id };
        playlist.VideoPlaylists.Add(new VideoPlaylist { PlaylistId = playlist.Id, VideoId = video.Id, Position = 0 });
        _dbContext.Playlists.Add(playlist);
        await _dbContext.SaveChangesAsync();

        SetCurrentUser(_admin.Id);

        var item = new PlaylistUpdateItem
        {
            Title = "P Updated",
            Filename = "p.json",
            Items = []
        };

        var result = await _controller.UpdatePlaylist(playlist.Id, item);
        Assert.That(result, Is.TypeOf<NoContentResult>());

        var updated = await _dbContext.Playlists.Include(p => p.VideoPlaylists).FirstAsync(p => p.Id == playlist.Id);
        Assert.That(updated.VideoPlaylists.Count, Is.EqualTo(0));
    }
}
