// MIT License
//
// Copyright (c) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;
using NUnit.Framework;

using System.Linq;
using System.Net;
using System.Threading.Tasks;

using MediaPi.Core.Controllers;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services;

namespace MediaPi.Core.Tests.Controllers;

[TestFixture]
public class DevicesControllerErrorTests
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
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"device_controller_error_test_db_{System.Guid.NewGuid()}")
            .Options;

        _dbContext = new AppDbContext(options);

        _adminRole = new Role { Id = (int)UserRoleConstants.SystemAdministrator, RoleId = UserRoleConstants.SystemAdministrator, Name = "Admin" };
        _managerRole = new Role { Id = (int)UserRoleConstants.AccountManager, RoleId = UserRoleConstants.AccountManager, Name = "Manager" };
        _engineerRole = new Role { Id = (int)UserRoleConstants.InstallationEngineer, RoleId = UserRoleConstants.InstallationEngineer, Name = "Engineer" };
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

        var d1 = new Device { Id = 1, Name = "Dev1", IpAddress = "1.1.1.1", AccountId = _account1.Id, DeviceGroupId = _group1.Id };
        var d2 = new Device { Id = 2, Name = "Dev2", IpAddress = "2.2.2.2" };
        var d3 = new Device { Id = 3, Name = "Dev3", IpAddress = "3.3.3.3", AccountId = _account2.Id, DeviceGroupId = _group2.Id };
        // Device with IPv6 address
        var d4 = new Device { Id = 4, Name = "Dev4", IpAddress = "2001:0db8:85a3:0000:0000:8a2e:0370:7334" };
        // Device with mapped IPv4 address
        var d5 = new Device { Id = 5, Name = "Dev5", IpAddress = "::ffff:192.168.1.1" };

        _dbContext.Users.AddRange(_admin, _manager, _engineer);
        _dbContext.Devices.AddRange(d1, d2, d3, d4, d5);
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
            _mockLogger.Object
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

    // Error case tests for GetAll endpoint
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
    public async Task GetAll_RegularUser_Returns403()
    {
        SetCurrentUser(4); // Regular user without admin, manager, or engineer role
        var result = await _controller.GetAll();
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    // Error case tests for GetAllByAccount endpoint
    [Test]
    public async Task GetAllByAccount_NoUser_Returns403()
    {
        SetCurrentUser(null);
        var result = await _controller.GetAllByAccount(1);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetAllByAccount_RegularUser_Returns403()
    {
        SetCurrentUser(4); // Regular user
        var result = await _controller.GetAllByAccount(1);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetAllByAccount_Manager_NullAccount_Returns403()
    {
        SetCurrentUser(2); // Manager
        var result = await _controller.GetAllByAccount(null);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    // Error case tests for GetAllByDeviceGroup endpoint
    [Test]
    public async Task GetAllByDeviceGroup_NoUser_Returns403()
    {
        SetCurrentUser(null);
        var result = await _controller.GetAllByDeviceGroup(1);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetAllByDeviceGroup_RegularUser_Returns403()
    {
        SetCurrentUser(4); // Regular user
        var result = await _controller.GetAllByDeviceGroup(1);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    // Error case tests for GetDevice endpoint
    [Test]
    public async Task GetDevice_NoUser_Returns403()
    {
        SetCurrentUser(null);
        var result = await _controller.GetDevice(1);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetDevice_RegularUser_Returns403()
    {
        SetCurrentUser(4); // Regular user
        var result = await _controller.GetDevice(1);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    // Error case tests for UpdateDevice endpoint
    [Test]
    public async Task UpdateDevice_NoUser_Returns403()
    {
        SetCurrentUser(null);
        var dto = new DeviceUpdateItem { Name = "Updated" };
        var result = await _controller.UpdateDevice(1, dto);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task UpdateDevice_InvalidIpv6_ReturnsBadRequest()
    {
        SetCurrentUser(1); // Admin
        var dto = new DeviceUpdateItem { IpAddress = "2001:0db8:85a3::invalid" };
        var result = await _controller.UpdateDevice(1, dto);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    // Error case tests for DeleteDevice endpoint
    [Test]
    public async Task DeleteDevice_NoUser_Returns403()
    {
        SetCurrentUser(null);
        var result = await _controller.DeleteDevice(1);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DeleteDevice_NonAdmin_Returns403()
    {
        // Test with manager user
        SetCurrentUser(2);
        var result = await _controller.DeleteDevice(1);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));

        // Test with engineer user
        SetCurrentUser(3);
        result = await _controller.DeleteDevice(1);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));

        // Test with regular user
        SetCurrentUser(4);
        result = await _controller.DeleteDevice(1);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    // Error case tests for AssignGroup endpoint
    [Test]
    public async Task AssignGroup_NoUser_Returns403()
    {
        SetCurrentUser(null);
        var dto = new DeviceAssignGroupItem { DeviceGroupId = 1 };
        var result = await _controller.AssignGroup(1, dto);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task AssignGroup_RegularUser_Returns403()
    {
        SetCurrentUser(4); // Regular user
        var dto = new DeviceAssignGroupItem { DeviceGroupId = 1 };
        var result = await _controller.AssignGroup(1, dto);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    // Error case tests for InitialAssignAccount endpoint
    [Test]
    public async Task InitialAssignAccount_NoUser_Returns403()
    {
        SetCurrentUser(null);
        var dto = new DeviceInitialAssignAccountItem { Name = "NewName", AccountId = 1 };
        var result = await _controller.InitialAssignAccount(1, dto);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task InitialAssignAccount_RegularUser_Returns403()
    {
        SetCurrentUser(4); // Regular user
        var dto = new DeviceInitialAssignAccountItem { Name = "NewName", AccountId = 1 };
        var result = await _controller.InitialAssignAccount(1, dto);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task InitialAssignAccount_ManagerCannotAssign_Returns403()
    {
        SetCurrentUser(2); // Manager
        var dto = new DeviceInitialAssignAccountItem { Name = "NewName", AccountId = 1 };
        var result = await _controller.InitialAssignAccount(2, dto);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    // Edge case tests
    [Test]
    public async Task Register_IPv6Address_CreatesDeviceWithIpv6()
    {
        SetCurrentUser(null, "2001:0db8:85a3:0000:0000:8a2e:0370:7335");
        var result = await _controller.Register();
        var created = result.Result as CreatedAtActionResult;
        Assert.That(created, Is.Not.Null);
        var reference = created!.Value as Reference;
        Assert.That(reference, Is.Not.Null);
        var dev = await _dbContext.Devices.FindAsync(reference!.Id);
        Assert.That(dev, Is.Not.Null);
        
        // Use IPAddress.Parse to get the expected standardized format
        string expectedFormat = IPAddress.Parse("2001:0db8:85a3:0000:0000:8a2e:0370:7335").ToString();
        Assert.That(dev!.IpAddress, Is.EqualTo(expectedFormat));
    }

    [Test]
    public async Task Update_DuplicateSameDevice_IpAddress_Succeeds()
    {
        // A device should be able to update with its own IP address
        SetCurrentUser(1); // Admin
        var dto = new DeviceUpdateItem { IpAddress = "1.1.1.1" };
        var result = await _controller.UpdateDevice(1, dto);
        Assert.That(result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task GetAllByDeviceGroup_Manager_NullButAssigned_Works()
    {
        // Create a new device with account but no group
        var newDevice = new Device { Id = 6, Name = "DevNoGroup", IpAddress = "6.6.6.6", AccountId = _account1.Id };
        _dbContext.Devices.Add(newDevice);
        await _dbContext.SaveChangesAsync();

        // Manager should be able to see ungrouped devices from their account
        SetCurrentUser(2); // Manager for account1
        var result = await _controller.GetAllByDeviceGroup(null);
        Assert.That(result.Value, Is.Not.Null);
        var devices = result.Value!.ToList();
        Assert.That(devices, Has.Count.EqualTo(1));
        Assert.That(devices[0].Id, Is.EqualTo(6));
    }
}