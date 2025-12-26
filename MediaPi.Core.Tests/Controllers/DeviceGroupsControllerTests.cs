// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;
using NUnit.Framework;

using System.Linq;
using System.Threading.Tasks;

using MediaPi.Core.Controllers;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services;

namespace MediaPi.Core.Tests.Controllers;

[TestFixture]
public class DeviceGroupsControllerTests
{
#pragma warning disable CS8618
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private Mock<ILogger<DeviceGroupsController>> _mockLogger;
    private AppDbContext _dbContext;
    private DeviceGroupsController _controller;
    private User _admin;
    private User _manager;
    private User _engineer;
    private Role _adminRole;
    private Role _managerRole;
    private Role _engineerRole;
    private Account _account1;
    private Account _account2;
    private DeviceGroup _group1;
    private DeviceGroup _group2;
    private Device _device1;
    private Playlist _playlist1;
    private Playlist _playlist2;
    private UserInformationService _userInformationService;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"device_group_controller_test_db_{System.Guid.NewGuid()}")
            .Options;

        _dbContext = new AppDbContext(options);

        _adminRole = new Role { RoleId = UserRoleConstants.SystemAdministrator, Name = "Admin" };
        _managerRole = new Role { RoleId = UserRoleConstants.AccountManager, Name = "Manager" };
        _engineerRole = new Role { RoleId = UserRoleConstants.InstallationEngineer, Name = "Engineer" };
        _dbContext.Roles.AddRange(_adminRole, _managerRole, _engineerRole);

        _account1 = new Account { Id = 1, Name = "Acc1" };
        _account2 = new Account { Id = 2, Name = "Acc2" };
        _dbContext.Accounts.AddRange(_account1, _account2);

        _group1 = new DeviceGroup { Id = 1, Name = "Grp1", AccountId = _account1.Id, Account = _account1 };
        _group2 = new DeviceGroup { Id = 2, Name = "Grp2", AccountId = _account2.Id, Account = _account2 };
        _dbContext.DeviceGroups.AddRange(_group1, _group2);

        _device1 = new Device { Id = 1, Name = "Dev1", IpAddress = "1.1.1.1", Port = 8080, AccountId = _account1.Id, DeviceGroupId = _group1.Id };
        _dbContext.Devices.Add(_device1);

        _playlist1 = new Playlist { Id = 1, Title = "Playlist1", Filename = "playlist1.m3u", AccountId = _account1.Id, Account = _account1 };
        _playlist2 = new Playlist { Id = 2, Title = "Playlist2", Filename = "playlist2.m3u", AccountId = _account1.Id, Account = _account1 };
        _dbContext.Playlists.AddRange(_playlist1, _playlist2);

        string pass = BCrypt.Net.BCrypt.HashPassword("pwd");

        _admin = new User
        {
            Id = 1,
            Email = "admin@example.com",
            Password = pass,
            UserRoles = [ new UserRole { UserId = 1, RoleId = _adminRole.Id, Role = _adminRole } ]
        };

        _manager = new User
        {
            Id = 2,
            Email = "manager@example.com",
            Password = pass,
            UserRoles = [ new UserRole { UserId = 2, RoleId = _managerRole.Id, Role = _managerRole } ],
            UserAccounts = [ new UserAccount { UserId = 2, AccountId = _account1.Id, Account = _account1 } ]
        };

        _engineer = new User
        {
            Id = 3,
            Email = "eng@example.com",
            Password = pass,
            UserRoles = [ new UserRole { UserId = 3, RoleId = _engineerRole.Id, Role = _engineerRole } ]
        };

        _dbContext.Users.AddRange(_admin, _manager, _engineer);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockLogger = new Mock<ILogger<DeviceGroupsController>>();
        _userInformationService = new UserInformationService(_dbContext);
    }

    private void SetCurrentUser(int? id)
    {
        var context = new DefaultHttpContext();
        if (id.HasValue) context.Items["UserId"] = id.Value;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);
        _controller = new DeviceGroupsController(
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

    // Existing tests
    [Test]
    public async Task GetAll_Admin_ReturnsAll()
    {
        SetCurrentUser(1);
        var result = await _controller.GetAll();
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task GetAll_Manager_ReturnsOwn()
    {
        SetCurrentUser(2);
        var result = await _controller.GetAll();
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(1));
        Assert.That(result.Value!.First().Id, Is.EqualTo(_group1.Id));
    }

    [Test]
    public async Task GetGroup_Manager_Other_Forbidden()
    {
        SetCurrentUser(2);
        var result = await _controller.GetGroup(_group2.Id);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DeleteGroup_Admin_SetsDeviceNull()
    {
        SetCurrentUser(1);
        var result = await _controller.DeleteGroup(_group1.Id);
        Assert.That(result, Is.TypeOf<NoContentResult>());
        var dev = await _dbContext.Devices.FindAsync(_device1.Id);
        Assert.That(dev!.DeviceGroupId, Is.Null);
        var grp = await _dbContext.DeviceGroups.FindAsync(_group1.Id);
        Assert.That(grp, Is.Null);
    }

    // New error case tests
    [Test]
    public async Task GetAll_NoUser_Returns403()
    {
        SetCurrentUser(null);
        var result = await _controller.GetAll();
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetAll_Engineer_Returns403()
    {
        SetCurrentUser(3); // Engineer
        var result = await _controller.GetAll();
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetGroup_NoUser_Returns403()
    {
        SetCurrentUser(null);
        var result = await _controller.GetGroup(_group1.Id);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetGroup_NotFound_Returns404()
    {
        SetCurrentUser(1); // Admin
        var result = await _controller.GetGroup(999);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetGroup_Engineer_Returns403()
    {
        SetCurrentUser(3); // Engineer
        var result = await _controller.GetGroup(_group1.Id);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task PostGroup_NoUser_Returns403()
    {
        SetCurrentUser(null);
        var dto = new DeviceGroupCreateItem { Name = "New Group", AccountId = _account1.Id, Playlists = [] };
        var result = await _controller.PostGroup(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task PostGroup_Engineer_Returns403()
    {
        SetCurrentUser(3); // Engineer
        var dto = new DeviceGroupCreateItem { Name = "New Group", AccountId = _account1.Id, Playlists = [] };
        var result = await _controller.PostGroup(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task PostGroup_Manager_OtherAccount_Returns403()
    {
        SetCurrentUser(2); // Manager with access to account1
        var dto = new DeviceGroupCreateItem { Name = "New Group", AccountId = _account2.Id, Playlists = [] };
        var result = await _controller.PostGroup(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task PostGroup_InvalidAccount_Returns404()
    {
        SetCurrentUser(1); // Admin
        var dto = new DeviceGroupCreateItem { Name = "New Group", AccountId = 999, Playlists = [] };
        var result = await _controller.PostGroup(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task UpdateGroup_NoUser_Returns403()
    {
        SetCurrentUser(null);
        var dto = new DeviceGroupUpdateItem { Name = "Updated" };
        var result = await _controller.UpdateGroup(_group1.Id, dto);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task UpdateGroup_NotFound_Returns404()
    {
        SetCurrentUser(1); // Admin
        var dto = new DeviceGroupUpdateItem { Name = "Updated" };
        var result = await _controller.UpdateGroup(999, dto);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task UpdateGroup_Engineer_Returns403()
    {
        SetCurrentUser(3); // Engineer
        var dto = new DeviceGroupUpdateItem { Name = "Updated" };
        var result = await _controller.UpdateGroup(_group1.Id, dto);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task UpdateGroup_Manager_OtherGroup_Returns403()
    {
        SetCurrentUser(2); // Manager with access to account1
        var dto = new DeviceGroupUpdateItem { Name = "Updated" };
        var result = await _controller.UpdateGroup(_group2.Id, dto);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DeleteGroup_NoUser_Returns403()
    {
        SetCurrentUser(null);
        var result = await _controller.DeleteGroup(_group1.Id);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DeleteGroup_NotFound_Returns404()
    {
        SetCurrentUser(1); // Admin
        var result = await _controller.DeleteGroup(999);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task DeleteGroup_Engineer_Returns403()
    {
        SetCurrentUser(3); // Engineer
        var result = await _controller.DeleteGroup(_group1.Id);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DeleteGroup_Manager_OtherGroup_Returns403()
    {
        SetCurrentUser(2); // Manager with access to account1
        var result = await _controller.DeleteGroup(_group2.Id);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DeleteGroup_Manager_OwnGroup_Succeeds()
    {
        SetCurrentUser(2); // Manager with access to account1
        var result = await _controller.DeleteGroup(_group1.Id);
        Assert.That(result, Is.TypeOf<NoContentResult>());
        var grp = await _dbContext.DeviceGroups.FindAsync(_group1.Id);
        Assert.That(grp, Is.Null);
    }

    [Test]
    public async Task UpdateGroup_Manager_CanUpdateName()
    {
        SetCurrentUser(2);
        var dto = new DeviceGroupUpdateItem { Name = "Renamed" };
        var result = await _controller.UpdateGroup(_group1.Id, dto);
        Assert.That(result, Is.TypeOf<NoContentResult>());
        var grp = await _dbContext.DeviceGroups.FindAsync(_group1.Id);
        Assert.That(grp!.AccountId, Is.EqualTo(_account1.Id)); // AccountId should remain unchanged
        Assert.That(grp.Name, Is.EqualTo("Renamed"));
    }

    [Test]
    public async Task PostGroup_Admin_CreatesPlaylistDeviceGroups()
    {
        SetCurrentUser(1);
        var dto = new DeviceGroupCreateItem
        {
            Name = "With Playlists",
            AccountId = _account1.Id,
            Playlists =
            [
                new PlaylistDeviceGroupItemDto { PlaylistId = _playlist1.Id, Play = true },
                new PlaylistDeviceGroupItemDto { PlaylistId = _playlist2.Id, Play = false }
            ]
        };

        var result = await _controller.PostGroup(dto);

        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var created = (CreatedAtActionResult)result.Result!;
        var reference = (Reference)created.Value!;
        var playlists = await _dbContext.Set<PlaylistDeviceGroup>()
            .Where(pdg => pdg.DeviceGroupId == reference.Id)
            .ToListAsync();
        Assert.That(playlists, Has.Count.EqualTo(2));
        Assert.That(playlists.Select(pdg => pdg.PlaylistId), Is.EquivalentTo(new[] { _playlist1.Id, _playlist2.Id }));
        Assert.That(playlists.Single(pdg => pdg.PlaylistId == _playlist1.Id).Play, Is.True);
        Assert.That(playlists.Single(pdg => pdg.PlaylistId == _playlist2.Id).Play, Is.False);
    }

    [Test]
    public async Task UpdateGroup_Manager_ReplacesPlaylists_WhenProvided()
    {
        _dbContext.Set<PlaylistDeviceGroup>().Add(new PlaylistDeviceGroup
        {
            DeviceGroupId = _group1.Id,
            PlaylistId = _playlist1.Id,
            Play = false
        });
        await _dbContext.SaveChangesAsync();

        SetCurrentUser(2);
        var dto = new DeviceGroupUpdateItem
        {
            Name = "Renamed",
            Playlists = [new PlaylistDeviceGroupItemDto { PlaylistId = _playlist2.Id, Play = true }]
        };
        var result = await _controller.UpdateGroup(_group1.Id, dto);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        var playlists = await _dbContext.Set<PlaylistDeviceGroup>()
            .Where(pdg => pdg.DeviceGroupId == _group1.Id)
            .ToListAsync();
        Assert.That(playlists, Has.Count.EqualTo(1));
        Assert.That(playlists[0].PlaylistId, Is.EqualTo(_playlist2.Id));
        Assert.That(playlists[0].Play, Is.True);
    }

    [Test]
    public async Task UpdateGroup_Manager_DoesNotReplacePlaylists_WhenNull()
    {
        _dbContext.Set<PlaylistDeviceGroup>().Add(new PlaylistDeviceGroup
        {
            DeviceGroupId = _group1.Id,
            PlaylistId = _playlist1.Id,
            Play = true
        });
        await _dbContext.SaveChangesAsync();

        SetCurrentUser(2);
        var dto = new DeviceGroupUpdateItem { Name = "Renamed", Playlists = null };
        var result = await _controller.UpdateGroup(_group1.Id, dto);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        var playlists = await _dbContext.Set<PlaylistDeviceGroup>()
            .Where(pdg => pdg.DeviceGroupId == _group1.Id)
            .ToListAsync();
        Assert.That(playlists, Has.Count.EqualTo(1));
        Assert.That(playlists[0].PlaylistId, Is.EqualTo(_playlist1.Id));
        Assert.That(playlists[0].Play, Is.True);
    }

    [Test]
    public async Task DeleteGroup_Admin_CascadesPlaylistDeviceGroups()
    {
        _dbContext.Set<PlaylistDeviceGroup>().Add(new PlaylistDeviceGroup
        {
            DeviceGroupId = _group1.Id,
            PlaylistId = _playlist1.Id,
            Play = true
        });
        await _dbContext.SaveChangesAsync();

        SetCurrentUser(1);
        var result = await _controller.DeleteGroup(_group1.Id);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        var playlists = await _dbContext.Set<PlaylistDeviceGroup>()
            .Where(pdg => pdg.DeviceGroupId == _group1.Id)
            .ToListAsync();
        Assert.That(playlists, Is.Empty);
    }

    [Test]
    public async Task GetAllByAccount_Admin_SpecificAccount_ReturnsGroups()
    {
        SetCurrentUser(1);
        var result = await _controller.GetAllByAccount(_account1.Id);
        Assert.That(result.Value, Is.Not.Null);
        var list = result.Value!.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Id, Is.EqualTo(_group1.Id));
    }

    [Test]
    public async Task GetAllByAccount_Admin_Null_ReturnsAll()
    {
        SetCurrentUser(1);
        var result = await _controller.GetAllByAccount(null);
        Assert.That(result.Value, Is.Not.Null);
        var list = result.Value!.ToList();
        Assert.That(list, Has.Count.EqualTo(2));
        Assert.That(list.Select(g => g.Id), Is.EquivalentTo(new[] { _group1.Id, _group2.Id }));
    }

    [Test]
    public async Task GetAllByAccount_Manager_OwnAccount_ReturnsGroups()
    {
        SetCurrentUser(2);
        var result = await _controller.GetAllByAccount(_account1.Id);
        Assert.That(result.Value, Is.Not.Null);
        var list = result.Value!.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Id, Is.EqualTo(_group1.Id));
    }

    [Test]
    public async Task GetAllByAccount_Manager_OtherAccount_Forbidden()
    {
        SetCurrentUser(2);
        var result = await _controller.GetAllByAccount(_account2.Id);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result.Result!;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetAllByAccount_Manager_Null_Forbidden()
    {
        SetCurrentUser(2);
        var result = await _controller.GetAllByAccount(null);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = (ObjectResult)result.Result!;
        Assert.That(obj.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }
}
