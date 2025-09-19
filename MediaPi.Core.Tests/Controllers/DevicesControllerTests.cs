// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;
using NUnit.Framework;

using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using MediaPi.Core.Controllers;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services;
using MediaPi.Core.Services.Models;
using MediaPi.Core.Services.Interfaces;

namespace MediaPi.Core.Tests.Controllers;

[TestFixture]
public class DevicesControllerTests
{
#pragma warning disable CS8618
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private Mock<ILogger<DevicesController>> _mockLogger;
    private AppDbContext _dbContext;
    private DevicesController _controller;
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
    private UserInformationService _userInformationService;
    private DeviceEventsService _deviceEventsService;
    private Mock<IDeviceMonitoringService> _monitoringServiceMock;
    private Mock<ISshClientKeyProvider> _sshKeyProviderMock;
    private Mock<IMediaPiAgentClient> _agentClientMock;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"device_controller_test_db_{System.Guid.NewGuid()}")
            .Options;

        _dbContext = new AppDbContext(options);
        _deviceEventsService = new DeviceEventsService();
        _monitoringServiceMock = new Mock<IDeviceMonitoringService>();
        _sshKeyProviderMock = new Mock<ISshClientKeyProvider>();
        _sshKeyProviderMock.Setup(p => p.GetPublicKey()).Returns("ssh-ed25519 AAAATESTSERVERPUBKEY test@server");
        _agentClientMock = new Mock<IMediaPiAgentClient>();

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

        string pass = BCrypt.Net.BCrypt.HashPassword("pwd");

        _admin = new User
        {
            Id = 1,
            Email = "admin@example.com",
            Password = pass,
            UserRoles = [new UserRole { UserId = 1, RoleId = _adminRole.Id, Role = _adminRole }]
        };

        _manager = new User
        {
            Id = 2,
            Email = "manager@example.com",
            Password = pass,
            UserRoles = [new UserRole { UserId = 2, RoleId = _managerRole.Id, Role = _managerRole }],
            UserAccounts = [new UserAccount { UserId = 2, AccountId = _account1.Id, Account = _account1 }]
        };

        _engineer = new User
        {
            Id = 3,
            Email = "eng@example.com",
            Password = pass,
            UserRoles = [new UserRole { UserId = 3, RoleId = _engineerRole.Id, Role = _engineerRole }]
        };

        var d1 = new Device { Id = 1, Name = "Dev1", IpAddress = "1.1.1.1", PublicKeyOpenSsh = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQDev1K8yG9aS2b+1wVbHgGhJ8T+Z3VhKJqGGH0YMiL8yG9aS2b+1wVbHgGhJ8T+Z3VhKJ", SshUser = "pi", AccountId = _account1.Id, DeviceGroupId = _group1.Id };
        var d2 = new Device { Id = 2, Name = "Dev2", IpAddress = "2.2.2.2", PublicKeyOpenSsh = string.Empty, SshUser = "pi" };
        var d3 = new Device { Id = 3, Name = "Dev3", IpAddress = "3.3.3.3", PublicKeyOpenSsh = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIDev3K8yG9aS2b+1wVbHgGhJ8T+Z3VhKJqGGH0YMiL8yG9aS", SshUser = "admin", AccountId = _account2.Id, DeviceGroupId = _group2.Id };

        _dbContext.Users.AddRange(_admin, _manager, _engineer);
        _dbContext.Devices.AddRange(d1, d2, d3);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockLogger = new Mock<ILogger<DevicesController>>();
        _userInformationService = new UserInformationService(_dbContext);
    }

    private void SetCurrentUser(int? id, string? ip = null)
    {
        var context = new DefaultHttpContext();
        if (id.HasValue) context.Items["UserId"] = id.Value;
        if (ip != null) context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);
        _controller = new DevicesController(
            _mockHttpContextAccessor.Object,
            _userInformationService,
            _dbContext,
            _mockLogger.Object,
            _deviceEventsService,
            _monitoringServiceMock.Object,
            _sshKeyProviderMock.Object,
            _agentClientMock.Object
        )
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
    public async Task Register_CreatesDeviceWithRemoteIp()
    {
        SetCurrentUser(null, "5.6.7.8");
        var req = new DeviceRegisterRequest 
        { 
            PublicKeyOpenSsh = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAINewKeyComplete123456789", 
            SshUser = "testuser" 
        };
        var result = await _controller.Register(req, CancellationToken.None);
        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var response = ok!.Value as DeviceRegisterResponse;
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.ServerPublicSshKey, Is.EqualTo("ssh-ed25519 AAAATESTSERVERPUBKEY test@server"));
        
        // Find device by IP address to verify it was created
        var dev = await _dbContext.Devices.FirstOrDefaultAsync(d => d.IpAddress == "5.6.7.8");
        Assert.That(dev, Is.Not.Null);
        Assert.That(dev!.IpAddress, Is.EqualTo("5.6.7.8"));
        Assert.That(dev.Name, Is.EqualTo($"Устройство №{dev.Id}"));
        Assert.That(dev.AccountId, Is.Null);
        Assert.That(dev.DeviceGroupId, Is.Null);
        Assert.That(dev.PublicKeyOpenSsh, Is.EqualTo("ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAINewKeyComplete123456789"));
        Assert.That(dev.SshUser, Is.EqualTo("testuser"));
    }

    [Test]
    public async Task Register_Ipv4Mapped_IpStoredAsIpv4()
    {
        SetCurrentUser(null, "::ffff:9.8.7.6");
        var req = new DeviceRegisterRequest 
        { 
            PublicKeyOpenSsh = string.Empty, 
            SshUser = null 
        };
        var result = await _controller.Register(req, CancellationToken.None);
        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var response = ok!.Value as DeviceRegisterResponse;
        Assert.That(response, Is.Not.Null);
        
        // Find device by IP address to verify it was created
        var dev = await _dbContext.Devices.FirstOrDefaultAsync(d => d.IpAddress == "9.8.7.6");
        Assert.That(dev, Is.Not.Null);
        Assert.That(dev!.IpAddress, Is.EqualTo("9.8.7.6"));
        Assert.That(dev.PublicKeyOpenSsh, Is.EqualTo(string.Empty));
        Assert.That(dev.SshUser, Is.EqualTo("pi")); // Should default to "pi"
    }

    [Test]
    public async Task Register_DuplicateIp_ReturnsConflict()
    {
        SetCurrentUser(null, "1.1.1.1");
        var req = new DeviceRegisterRequest 
        { 
            PublicKeyOpenSsh = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIExample", 
            SshUser = "user1" 
        };
        var result = await _controller.Register(req, CancellationToken.None);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task Register_NoIp_ReturnsBadRequest()
    {
        SetCurrentUser(null);
        var req = new DeviceRegisterRequest 
        { 
            PublicKeyOpenSsh = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQC7NoIpTestComplete123456789012345678901234567890123456789012345678", 
            SshUser = "noipuser" 
        };
        var result = await _controller.Register(req, CancellationToken.None);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task Register_UsesProvidedNameAndIpAddress()
    {
        SetCurrentUser(null, "5.5.5.5");
        var req = new DeviceRegisterRequest
        {
            PublicKeyOpenSsh = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAITestKeyProvided",
            SshUser = "user",
            Name = "Provided Name",
            IpAddress = "8.7.6.5"
        };
        var result = await _controller.Register(req, CancellationToken.None);
        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var response = ok!.Value as DeviceRegisterResponse;
        Assert.That(response, Is.Not.Null);

        var dev = await _dbContext.Devices.FirstOrDefaultAsync(d => d.Id == response!.Id);
        Assert.That(dev, Is.Not.Null);
        Assert.That(dev!.IpAddress, Is.EqualTo("8.7.6.5"));
        Assert.That(dev.Name, Is.EqualTo("Provided Name"));
    }

    [Test]
    public async Task Register_IpProvidedWithoutName_AssignsDefaultName()
    {
        SetCurrentUser(null, "4.4.4.4");
        var req = new DeviceRegisterRequest
        {
            PublicKeyOpenSsh = string.Empty,
            SshUser = "user",
            IpAddress = "9.9.9.9"
        };
        var result = await _controller.Register(req, CancellationToken.None);
        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var response = ok!.Value as DeviceRegisterResponse;
        Assert.That(response, Is.Not.Null);

        var dev = await _dbContext.Devices.FirstOrDefaultAsync(d => d.Id == response!.Id);
        Assert.That(dev, Is.Not.Null);
        Assert.That(dev!.IpAddress, Is.EqualTo("9.9.9.9"));
        Assert.That(dev.Name, Is.EqualTo($"Устройство №{dev.Id}"));
    }

    [Test]
    public async Task Register_MalformedIp_ReturnsBadRequest()
    {
        SetCurrentUser(null, "3.3.3.3");
        var req = new DeviceRegisterRequest
        {
            PublicKeyOpenSsh = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAABadIp",
            SshUser = "user",
            IpAddress = "not.an.ip"
        };
        var result = await _controller.Register(req, CancellationToken.None);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetAll_Admin_ReturnsAll()
    {
        SetCurrentUser(1);
        var result = await _controller.GetAll();
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(3));
    }

    [Test]
    public async Task GetAll_Manager_FiltersByAccount()
    {
        SetCurrentUser(2);
        var result = await _controller.GetAll();
        Assert.That(result.Value, Is.Not.Null);
        var list = result.Value!.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Id, Is.EqualTo(1));
    }

    [Test]
    public async Task GetAll_Engineer_ReturnsUnassigned()
    {
        SetCurrentUser(3);
        var result = await _controller.GetAll();
        Assert.That(result.Value, Is.Not.Null);
        var list = result.Value!.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Id, Is.EqualTo(2));
    }

    [Test]
    public async Task GetAll_IncludesDeviceStatus()
    {
        var snapshot = new DeviceStatusSnapshot
        {
            IpAddress = "1.1.1.1",
            IsOnline = true,
            LastChecked = DateTime.UtcNow,
            ConnectLatencyMs = 1,
            TotalLatencyMs = 2
        };
        _monitoringServiceMock.Setup(s => s.TryGetStatusItem(1, out It.Ref<DeviceStatusItem?>.IsAny))
            .Returns((int id, out DeviceStatusItem? status) => { status = new DeviceStatusItem(id, snapshot); return true; });

        SetCurrentUser(1);
        var result = await _controller.GetAll();
        var item = result.Value!.First(d => d.Id == 1);
        Assert.That(item.DeviceStatus, Is.Not.Null);
        Assert.That(item.DeviceStatus!.IsOnline, Is.True);
        Assert.That(item.SshUser, Is.EqualTo("pi")); // Test data value
    }

    [Test]
    public async Task GetAllByAccount_Admin_SpecificAccount()
    {
        SetCurrentUser(1);
        var result = await _controller.GetAllByAccount(_account1.Id);
        Assert.That(result.Value, Is.Not.Null);
        var list = result.Value!.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Id, Is.EqualTo(1));
    }

    [Test]
    public async Task GetAllByAccount_Admin_Null_ReturnsUnassigned()
    {
        SetCurrentUser(1);
        var result = await _controller.GetAllByAccount(null);
        Assert.That(result.Value, Is.Not.Null);
        var list = result.Value!.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Id, Is.EqualTo(2));
    }

    [Test]
    public async Task GetAllByAccount_Manager_OwnAccount()
    {
        SetCurrentUser(2);
        var result = await _controller.GetAllByAccount(_account1.Id);
        Assert.That(result.Value, Is.Not.Null);
        var list = result.Value!.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Id, Is.EqualTo(1));
    }

    [Test]
    public async Task GetAllByAccount_Manager_OtherAccount_Forbidden()
    {
        SetCurrentUser(2);
        var result = await _controller.GetAllByAccount(_account2.Id);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetAllByAccount_Engineer_Unassigned()
    {
        SetCurrentUser(3);
        var result = await _controller.GetAllByAccount(null);
        Assert.That(result.Value, Is.Not.Null);
        var list = result.Value!.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Id, Is.EqualTo(2));
    }

    [Test]
    public async Task GetAllByAccount_Engineer_NotNull_Forbidden()
    {
        SetCurrentUser(3);
        var result = await _controller.GetAllByAccount(_account1.Id);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetAllByDeviceGroup_Admin_SpecificGroup()
    {
        SetCurrentUser(1);
        var result = await _controller.GetAllByDeviceGroup(_group1.Id);
        Assert.That(result.Value, Is.Not.Null);
        var list = result.Value!.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Id, Is.EqualTo(1));
    }

    [Test]
    public async Task GetAllByDeviceGroup_Admin_Null_ReturnsUngrouped()
    {
        SetCurrentUser(1);
        var result = await _controller.GetAllByDeviceGroup(null);
        Assert.That(result.Value, Is.Not.Null);
        var list = result.Value!.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Id, Is.EqualTo(2));
    }

    [Test]
    public async Task GetAllByDeviceGroup_Manager_OwnGroup()
    {
        SetCurrentUser(2);
        var result = await _controller.GetAllByDeviceGroup(_group1.Id);
        Assert.That(result.Value, Is.Not.Null);
        var list = result.Value!.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Id, Is.EqualTo(1));
    }

    [Test]
    public async Task GetAllByDeviceGroup_Manager_OtherGroup_Forbidden()
    {
        SetCurrentUser(2);
        var result = await _controller.GetAllByDeviceGroup(_group2.Id);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetAllByDeviceGroup_Engineer_Unassigned()
    {
        SetCurrentUser(3);
        var result = await _controller.GetAllByDeviceGroup(null);
        Assert.That(result.Value, Is.Not.Null);
        var list = result.Value!.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Id, Is.EqualTo(2));
    }

    [Test]
    public async Task GetAllByDeviceGroup_Engineer_Group_Forbidden()
    {
        SetCurrentUser(3);
        var result = await _controller.GetAllByDeviceGroup(_group1.Id);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task Get_Admin_CanRetrieveAny()
    {
        SetCurrentUser(1);
        var result = await _controller.GetDevice(3);
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Id, Is.EqualTo(3));
        Assert.That(result.Value.SshUser, Is.EqualTo("admin")); // Test data value
    }

    [Test]
    public async Task Get_Manager_OwnDevice_Succeeds()
    {
        SetCurrentUser(2);
        var result = await _controller.GetDevice(1);
        Assert.That(result.Value, Is.Not.Null);
    }

    [Test]
    public async Task Get_Manager_OtherDevice_Forbidden()
    {
        SetCurrentUser(2);
        var result = await _controller.GetDevice(3);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task Get_Engineer_Unassigned_Succeeds()
    {
        SetCurrentUser(3);
        var result = await _controller.GetDevice(2);
        Assert.That(result.Value, Is.Not.Null);
    }

    [Test]
    public async Task Get_Engineer_Assigned_Forbidden()
    {
        SetCurrentUser(3);
        var result = await _controller.GetDevice(1);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetDevice_ReturnsDeviceStatus_WhenAvailable()
    {
        var snapshot = new DeviceStatusSnapshot
        {
            IpAddress = "1.1.1.1",
            IsOnline = true,
            LastChecked = DateTime.UtcNow,
            ConnectLatencyMs = 1,
            TotalLatencyMs = 2
        };
        _monitoringServiceMock.Setup(s => s.TryGetStatusItem(1, out It.Ref<DeviceStatusItem?>.IsAny))

                .Returns((int id, out DeviceStatusItem? status) => { status = new DeviceStatusItem(id, snapshot); return true; });

        SetCurrentUser(1);
        var result = await _controller.GetDevice(1);
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.DeviceStatus, Is.Not.Null);
        Assert.That(result.Value!.DeviceStatus!.IsOnline, Is.True);
    }

    [Test]
    public async Task Update_Admin_ValidIp_Succeeds()
    {
        SetCurrentUser(1);
        var dto = new DeviceUpdateItem { IpAddress = "10.0.0.1" };
        var response = await _controller.UpdateDevice(1, dto);
        Assert.That(response, Is.TypeOf<NoContentResult>());
        var dev = await _dbContext.Devices.FindAsync(1);
        Assert.That(dev!.IpAddress, Is.EqualTo("10.0.0.1"));
    }

    [Test]
    public async Task Update_Admin_Ipv4MappedIp_StoredAsIpv4()
    {
        SetCurrentUser(1);
        var dto = new DeviceUpdateItem { IpAddress = "::ffff:10.0.0.2" };
        var response = await _controller.UpdateDevice(1, dto);
        Assert.That(response, Is.TypeOf<NoContentResult>());
        var dev = await _dbContext.Devices.FindAsync(1);
        Assert.That(dev!.IpAddress, Is.EqualTo("10.0.0.2"));
    }

    [Test]
    public async Task Update_Admin_DuplicateIp_ReturnsConflict()
    {
        SetCurrentUser(1);
        var dto = new DeviceUpdateItem { IpAddress = "2.2.2.2" }; // existing ip of device 2
        var response = await _controller.UpdateDevice(1, dto);
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task Update_Admin_InvalidIp_ReturnsBadRequest()
    {
        SetCurrentUser(1);
        var dto = new DeviceUpdateItem { IpAddress = "bad-ip" };
        var response = await _controller.UpdateDevice(1, dto);
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task Update_NotAdmin_ReturnsForbidden()
    {
        SetCurrentUser(2);
        var dto = new DeviceUpdateItem { IpAddress = "10.0.0.1" };
        var response = await _controller.UpdateDevice(1, dto);
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task Update_Admin_AccountIdZero_SetsAccountIdToNull()
    {
        SetCurrentUser(1);
        var dto = new DeviceUpdateItem { AccountId = 0 };
        var response = await _controller.UpdateDevice(1, dto); // Device 1 has AccountId = _account1.Id
        Assert.That(response, Is.TypeOf<NoContentResult>());
        var dev = await _dbContext.Devices.FindAsync(1);
        Assert.That(dev!.AccountId, Is.Null);
    }

    [Test]
    public async Task Update_Admin_AccountIdValid_SetsAccountId()
    {
        SetCurrentUser(1);
        var dto = new DeviceUpdateItem { AccountId = _account2.Id };
        var response = await _controller.UpdateDevice(2, dto); // Device 2 initially has no account
        Assert.That(response, Is.TypeOf<NoContentResult>());
        var dev = await _dbContext.Devices.FindAsync(2);
        Assert.That(dev!.AccountId, Is.EqualTo(_account2.Id));
    }

    [Test]
    public async Task Update_Admin_AccountIdZero_OnUnassignedDevice_RemainsNull()
    {
        SetCurrentUser(1);
        var dto = new DeviceUpdateItem { AccountId = 0 };
        var response = await _controller.UpdateDevice(2, dto); // Device 2 initially has no account
        Assert.That(response, Is.TypeOf<NoContentResult>());
        var dev = await _dbContext.Devices.FindAsync(2);
        Assert.That(dev!.AccountId, Is.Null);
    }

    [Test]
    public async Task Update_Admin_CombinedUpdate_UpdatesMultipleFields()
    {
        SetCurrentUser(1);
        var dto = new DeviceUpdateItem 
        { 
            Name = "UpdatedDevice", 
            IpAddress = "192.168.1.100",
            SshUser = "updateduser",
            AccountId = 0, // Unassign from account
            DeviceGroupId = 0 // Unassign from group
        };
        var response = await _controller.UpdateDevice(1, dto, CancellationToken.None); // Device 1 has all fields set
        Assert.That(response, Is.TypeOf<NoContentResult>());
        var dev = await _dbContext.Devices.FindAsync(1);
        Assert.That(dev!.Name, Is.EqualTo("UpdatedDevice"));
        Assert.That(dev.IpAddress, Is.EqualTo("192.168.1.100"));
        // No change to PublicKeyOpenSsh
        Assert.That(dev.PublicKeyOpenSsh, Is.EqualTo("ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQDev1K8yG9aS2b+1wVbHgGhJ8T+Z3VhKJqGGH0YMiL8yG9aS2b+1wVbHgGhJ8T+Z3VhKJ"));
        Assert.That(dev.SshUser, Is.EqualTo("updateduser"));
        Assert.That(dev.AccountId, Is.Null);
        Assert.That(dev.DeviceGroupId, Is.Null);
            }

    [Test]
    public async Task AssignGroup_Admin_Succeeds()
    {
        SetCurrentUser(1);
        // First change device 1 to account 2, so we can assign group 2 (which belongs to account 2)
        var device = await _dbContext.Devices.FindAsync(1);
        device!.AccountId = _account2.Id;
        await _dbContext.SaveChangesAsync();
        
        var dto = new Reference { Id = _group2.Id }; // Group2 belongs to Account2, Device1 now belongs to Account2
        var response = await _controller.AssignGroup(1, dto);
        Assert.That(response, Is.TypeOf<NoContentResult>());
        var dev = await _dbContext.Devices.FindAsync(1);
        Assert.That(dev!.DeviceGroupId, Is.EqualTo(_group2.Id));
    }

    [Test]
    public async Task AssignGroup_Manager_OwnDevice_Succeeds()
    {
        SetCurrentUser(2);
        var dto = new Reference { Id = _group1.Id }; // Group1 belongs to Account1, Device1 belongs to Account1 (manager owns both)
        var response = await _controller.AssignGroup(1, dto);
        Assert.That(response, Is.TypeOf<NoContentResult>());
        var dev = await _dbContext.Devices.FindAsync(1);
        Assert.That(dev!.DeviceGroupId, Is.EqualTo(_group1.Id));
    }

    [Test]
    public async Task AssignGroup_Manager_OtherDevice_Forbidden()
    {
        SetCurrentUser(2);
        var dto = new Reference { Id = _group1.Id }; // Valid group, but device 3 doesn't belong to manager
        var response = await _controller.AssignGroup(3, dto);
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task AssignGroup_Engineer_Forbidden()
    {
        SetCurrentUser(3);
        var dto = new Reference { Id = _group1.Id }; // Valid group, but engineer doesn't have permission
        var response = await _controller.AssignGroup(2, dto);
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task AssignGroup_NotFound_Returns404()
    {
        SetCurrentUser(1);
        var dto = new Reference { Id = 1 };
        var response = await _controller.AssignGroup(999, dto);
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task AssignAccount_Admin_Succeeds()
    {
        SetCurrentUser(1);
        var dto = new Reference { Id = _account1.Id };
        var response = await _controller.AssignAccount(2, dto);
        Assert.That(response, Is.TypeOf<NoContentResult>());
        var dev = await _dbContext.Devices.FindAsync(2);
        Assert.That(dev!.AccountId, Is.EqualTo(_account1.Id));
    }

    [Test]
    public async Task AssignAccount_Engineer_Unassigned_Succeeds()
    {
        SetCurrentUser(3);
        var dto = new Reference { Id = _account1.Id };
        var response = await _controller.AssignAccount(2, dto);
        Assert.That(response, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task AssignAccount_Engineer_Assigned_Forbidden()
    {
        SetCurrentUser(3);
        var dto = new Reference { Id = _account1.Id };
        var response = await _controller.AssignAccount(1, dto);
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task AssignAccount_Manager_Forbidden()
    {
        SetCurrentUser(2);
        var dto = new Reference { Id = _account1.Id };
        var response = await _controller.AssignAccount(2, dto);
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task Delete_Admin_Succeeds()
    {
        SetCurrentUser(1);
        var response = await _controller.DeleteDevice(1);
        Assert.That(response, Is.TypeOf<NoContentResult>());
        Assert.That(await _dbContext.Devices.FindAsync(1), Is.Null);
    }

    [Test]
    public async Task Delete_NotAdmin_Forbidden()
    {
        SetCurrentUser(2);
        var response = await _controller.DeleteDevice(1);
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task Get_NotFound_Returns404()
    {
        SetCurrentUser(1);
        var result = await _controller.GetDevice(999);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task Update_NotFound_Returns404()
    {
        SetCurrentUser(1);
        var response = await _controller.UpdateDevice(999, new DeviceUpdateItem());
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task Delete_NotFound_Returns404()
    {
        SetCurrentUser(1);
        var response = await _controller.DeleteDevice(999);
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task AssignAccount_NotFound_Returns404()
    {
        SetCurrentUser(1);
        var dto = new Reference { Id = _account1.Id };
        var response = await _controller.AssignAccount(999, dto);
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task AssignAccount_Admin_ZeroId_SetsAccountIdToNull()
    {
        SetCurrentUser(1);
        var dto = new Reference { Id = 0 };
        var response = await _controller.AssignAccount(1, dto); // Device 1 has AccountId = _account1.Id
        Assert.That(response, Is.TypeOf<NoContentResult>());
        var dev = await _dbContext.Devices.FindAsync(1);
        Assert.That(dev!.AccountId, Is.Null);
    }

    [Test]
    public async Task AssignAccount_Engineer_ZeroId_SetsAccountIdToNull()
    {
        SetCurrentUser(3);
        // First assign an account to device 2 so we can test unassignment
        var assignDevice = await _dbContext.Devices.FindAsync(2);
        assignDevice!.AccountId = _account1.Id;
        await _dbContext.SaveChangesAsync();
        
        // Now try to unassign with engineer (should be forbidden since device is now assigned)
        var dto = new Reference { Id = 0 };
        var response = await _controller.AssignAccount(2, dto);
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task AssignAccount_Admin_AccountChange_SetsDeviceGroupIdToNull()
    {
        SetCurrentUser(1);
        // Device 1 initially has AccountId = _account1.Id and DeviceGroupId = _group1.Id
        var dto = new Reference { Id = _account2.Id }; // Change to different account
        var response = await _controller.AssignAccount(1, dto);
        Assert.That(response, Is.TypeOf<NoContentResult>());
        var dev = await _dbContext.Devices.FindAsync(1);
        Assert.That(dev!.AccountId, Is.EqualTo(_account2.Id));
        Assert.That(dev.DeviceGroupId, Is.Null); // Should be null when account changes
    }

    [Test]
    public async Task AssignAccount_Admin_SameAccount_KeepsDeviceGroupId()
    {
        SetCurrentUser(1);
        // Device 1 initially has AccountId = _account1.Id and DeviceGroupId = _group1.Id
        var dto = new Reference { Id = _account1.Id }; // Same account
        var response = await _controller.AssignAccount(1, dto);
        Assert.That(response, Is.TypeOf<NoContentResult>());
        var dev = await _dbContext.Devices.FindAsync(1);
        Assert.That(dev!.AccountId, Is.EqualTo(_account1.Id));
        Assert.That(dev.DeviceGroupId, Is.EqualTo(_group1.Id)); // Should keep the group when account doesn't change
    }

    [Test]
    public async Task AssignAccount_Admin_ZeroIdFromAccount_ClearsDeviceGroupId()
    {
        SetCurrentUser(1);
        // Device 1 initially has AccountId = _account1.Id and DeviceGroupId = _group1.Id
        var dto = new Reference { Id = 0 }; // Unassign account
        var response = await _controller.AssignAccount(1, dto);
        Assert.That(response, Is.TypeOf<NoContentResult>());
        var dev = await _dbContext.Devices.FindAsync(1);
        Assert.That(dev!.AccountId, Is.Null);
        Assert.That(dev.DeviceGroupId, Is.Null); // Should be null when account changes to null
    }

    [Test]
    public async Task AssignGroup_Admin_ZeroId_SetsDeviceGroupIdToNull()
    {
        SetCurrentUser(1);
        var dto = new Reference { Id = 0 };
        var response = await _controller.AssignGroup(1, dto); // Device 1 has DeviceGroupId = _group1.Id
        Assert.That(response, Is.TypeOf<NoContentResult>());
        var dev = await _dbContext.Devices.FindAsync(1);
        Assert.That(dev!.DeviceGroupId, Is.Null);
    }

    [Test]
    public async Task AssignGroup_Manager_ZeroId_SetsDeviceGroupIdToNull()
    {
        SetCurrentUser(2);
        var dto = new Reference { Id = 0 };
        var response = await _controller.AssignGroup(1, dto); // Device 1 is owned by manager and has DeviceGroupId = _group1.Id
        Assert.That(response, Is.TypeOf<NoContentResult>());
        var dev = await _dbContext.Devices.FindAsync(1);
        Assert.That(dev!.DeviceGroupId, Is.Null);
    }

    [Test]
    public async Task AssignGroup_Admin_ValidGroupSameAccount_Succeeds()
    {
        SetCurrentUser(1);
        var dto = new Reference { Id = _group1.Id }; // Group1 belongs to Account1, Device1 belongs to Account1
        var response = await _controller.AssignGroup(1, dto);
        Assert.That(response, Is.TypeOf<NoContentResult>());
        var dev = await _dbContext.Devices.FindAsync(1);
        Assert.That(dev!.DeviceGroupId, Is.EqualTo(_group1.Id));
    }

    [Test]
    public async Task AssignGroup_Admin_GroupAccountMismatch_ReturnsConflict()
    {
        SetCurrentUser(1);
        var dto = new Reference { Id = _group2.Id }; // Group2 belongs to Account2, but Device1 belongs to Account1
        var response = await _controller.AssignGroup(1, dto);
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task AssignGroup_Admin_DeviceUnassigned_GroupAssigned_ReturnsConflict()
    {
        SetCurrentUser(1);
        var dto = new Reference { Id = _group1.Id }; // Group1 belongs to Account1, but Device2 has no account
        var response = await _controller.AssignGroup(2, dto);
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task AssignGroup_Manager_GroupAccountMismatch_ReturnsConflict()
    {
        SetCurrentUser(2);
        var dto = new Reference { Id = _group2.Id }; // Group2 belongs to Account2, but Device1 (owned by manager) belongs to Account1
        var response = await _controller.AssignGroup(1, dto);
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task AssignGroup_Admin_NonExistentGroup_ReturnsNotFound()
    {
        SetCurrentUser(1);
        var dto = new Reference { Id = 999 }; // Non-existent group
        var response = await _controller.AssignGroup(1, dto);
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task ListServices_Admin_ReturnsAgentResponse()
    {
        SetCurrentUser(_admin.Id);
        var agentResponse = new MediaPiAgentListResponse
        {
            Ok = true,
            Units =
            [
                new MediaPiAgentListUnit { Unit = "svc1" }
            ]
        };

        _agentClientMock
            .Setup(c => c.ListUnitsAsync(It.Is<Device>(d => d.Id == 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentResponse);

        var result = await _controller.ListServices(1, CancellationToken.None);

        var okResult = result.Result as OkObjectResult;
        Assert.That(okResult, Is.Not.Null);
        Assert.That(okResult!.Value, Is.SameAs(agentResponse));

        _agentClientMock.Verify(c => c.ListUnitsAsync(It.Is<Device>(d => d.Id == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ListServices_ManagerOwnsDevice_ReturnsAgentResponse()
    {
        SetCurrentUser(_manager.Id);
        var response = new MediaPiAgentListResponse { Ok = true };
        _agentClientMock
            .Setup(c => c.ListUnitsAsync(It.Is<Device>(d => d.Id == 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.ListServices(1, CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That(ok!.Value, Is.SameAs(response));
    }

    [Test]
    public async Task ListServices_EngineerUnassignedDevice_ReturnsAgentResponse()
    {
        SetCurrentUser(_engineer.Id);
        var response = new MediaPiAgentListResponse { Ok = true };
        _agentClientMock
            .Setup(c => c.ListUnitsAsync(It.Is<Device>(d => d.Id == 2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.ListServices(2, CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That(ok!.Value, Is.SameAs(response));
    }

    [Test]
    public async Task StartService_Admin_InvokesAgent()
    {
        SetCurrentUser(_admin.Id);
        var agentResponse = new MediaPiAgentUnitResultResponse { Ok = true, Unit = "svc", Result = "started" };
        _agentClientMock
            .Setup(c => c.StartUnitAsync(It.Is<Device>(d => d.Id == 1), "svc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentResponse);

        var result = await _controller.StartService(1, "svc", CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That(ok!.Value, Is.SameAs(agentResponse));
    }

    [Test]
    public async Task StopService_Admin_InvokesAgent()
    {
        SetCurrentUser(_admin.Id);
        var agentResponse = new MediaPiAgentUnitResultResponse { Ok = true, Unit = "svc", Result = "stopped" };
        _agentClientMock
            .Setup(c => c.StopUnitAsync(It.Is<Device>(d => d.Id == 1), "svc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentResponse);

        var result = await _controller.StopService(1, "svc", CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That(ok!.Value, Is.SameAs(agentResponse));
    }

    [Test]
    public async Task RestartService_Admin_InvokesAgent()
    {
        SetCurrentUser(_admin.Id);
        var agentResponse = new MediaPiAgentUnitResultResponse { Ok = true, Unit = "svc", Result = "restarted" };
        _agentClientMock
            .Setup(c => c.RestartUnitAsync(It.Is<Device>(d => d.Id == 1), "svc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentResponse);

        var result = await _controller.RestartService(1, "svc", CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That(ok!.Value, Is.SameAs(agentResponse));
    }

    [Test]
    public async Task EnableService_Admin_InvokesAgent()
    {
        SetCurrentUser(_admin.Id);
        var agentResponse = new MediaPiAgentEnableResponse { Ok = true, Unit = "svc", Enabled = true };
        _agentClientMock
            .Setup(c => c.EnableUnitAsync(It.Is<Device>(d => d.Id == 1), "svc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentResponse);

        var result = await _controller.EnableService(1, "svc", CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That(ok!.Value, Is.SameAs(agentResponse));
    }

    [Test]
    public async Task DisableService_Admin_InvokesAgent()
    {
        SetCurrentUser(_admin.Id);
        var agentResponse = new MediaPiAgentEnableResponse { Ok = true, Unit = "svc", Enabled = false };
        _agentClientMock
            .Setup(c => c.DisableUnitAsync(It.Is<Device>(d => d.Id == 1), "svc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentResponse);

        var result = await _controller.DisableService(1, "svc", CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That(ok!.Value, Is.SameAs(agentResponse));
    }

}
