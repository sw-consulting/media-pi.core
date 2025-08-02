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
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"device_controller_test_db_{System.Guid.NewGuid()}")
            .Options;

        _dbContext = new AppDbContext(options);

        _adminRole = new Role { RoleId = UserRoleConstants.SystemAdministrator, Name = "Admin" };
        _managerRole = new Role { RoleId = UserRoleConstants.AccountManager, Name = "Manager" };
        _engineerRole = new Role { RoleId = UserRoleConstants.InstallationEngineer, Name = "Engineer" };
        _dbContext.Roles.AddRange(_adminRole, _managerRole, _engineerRole);

        _account1 = new Account { Id = 1, Name = "Acc1" };
        _account2 = new Account { Id = 2, Name = "Acc2" };
        _dbContext.Accounts.AddRange(_account1, _account2);

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

        var d1 = new Device { Id = 1, Name = "Dev1", IpAddress = "1.1.1.1", AccountId = _account1.Id };
        var d2 = new Device { Id = 2, Name = "Dev2", IpAddress = "2.2.2.2" };
        var d3 = new Device { Id = 3, Name = "Dev3", IpAddress = "3.3.3.3", AccountId = _account2.Id };

        _dbContext.Users.AddRange(_admin, _manager, _engineer);
        _dbContext.Devices.AddRange(d1, d2, d3);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockLogger = new Mock<ILogger<DevicesController>>();
    }

    private void SetCurrentUser(int? id, string? ip = null)
    {
        var context = new DefaultHttpContext();
        if (id.HasValue) context.Items["UserId"] = id.Value;
        if (ip != null) context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);
        _controller = new DevicesController(_mockHttpContextAccessor.Object, _dbContext, _mockLogger.Object)
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
        var result = await _controller.Register();
        var created = result.Result as CreatedAtActionResult;
        Assert.That(created, Is.Not.Null);
        var reference = created!.Value as Reference;
        Assert.That(reference, Is.Not.Null);
        var dev = await _dbContext.Devices.FindAsync(reference!.Id);
        Assert.That(dev, Is.Not.Null);
        Assert.That(dev!.IpAddress, Is.EqualTo("5.6.7.8"));
        Assert.That(dev.Name, Is.EqualTo($"Устройство №{dev.Id}"));
        Assert.That(dev.AccountId, Is.Null);
        Assert.That(dev.DeviceGroupId, Is.Null);
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
    public async Task Get_Admin_CanRetrieveAny()
    {
        SetCurrentUser(1);
        var result = await _controller.GetDevice(3);
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Id, Is.EqualTo(3));
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
    public async Task AssignGroup_Admin_Succeeds()
    {
        SetCurrentUser(1);
        var dto = new DeviceAssignGroupItem { DeviceGroupId = 5 };
        var response = await _controller.AssignGroup(1, dto);
        Assert.That(response, Is.TypeOf<NoContentResult>());
        var dev = await _dbContext.Devices.FindAsync(1);
        Assert.That(dev!.DeviceGroupId, Is.EqualTo(5));
    }

    [Test]
    public async Task AssignGroup_Manager_OwnDevice_Succeeds()
    {
        SetCurrentUser(2);
        var dto = new DeviceAssignGroupItem { DeviceGroupId = 7 };
        var response = await _controller.AssignGroup(1, dto);
        Assert.That(response, Is.TypeOf<NoContentResult>());
        var dev = await _dbContext.Devices.FindAsync(1);
        Assert.That(dev!.DeviceGroupId, Is.EqualTo(7));
    }

    [Test]
    public async Task AssignGroup_Manager_OtherDevice_Forbidden()
    {
        SetCurrentUser(2);
        var dto = new DeviceAssignGroupItem { DeviceGroupId = 7 };
        var response = await _controller.AssignGroup(3, dto);
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task AssignGroup_Engineer_Forbidden()
    {
        SetCurrentUser(3);
        var dto = new DeviceAssignGroupItem { DeviceGroupId = 7 };
        var response = await _controller.AssignGroup(2, dto);
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task InitialAssignAccount_Admin_Succeeds()
    {
        SetCurrentUser(1);
        var dto = new DeviceInitialAssignAccountItem { Name = "NewName", AccountId = _account1.Id };
        var response = await _controller.InitialAssignAccount(2, dto);
        Assert.That(response, Is.TypeOf<NoContentResult>());
        var dev = await _dbContext.Devices.FindAsync(2);
        Assert.That(dev!.AccountId, Is.EqualTo(_account1.Id));
        Assert.That(dev.Name, Is.EqualTo("NewName"));
    }

    [Test]
    public async Task InitialAssignAccount_Engineer_Unassigned_Succeeds()
    {
        SetCurrentUser(3);
        var dto = new DeviceInitialAssignAccountItem { Name = "Init", AccountId = _account1.Id };
        var response = await _controller.InitialAssignAccount(2, dto);
        Assert.That(response, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task InitialAssignAccount_Engineer_Assigned_Forbidden()
    {
        SetCurrentUser(3);
        var dto = new DeviceInitialAssignAccountItem { Name = "Init", AccountId = _account1.Id };
        var response = await _controller.InitialAssignAccount(1, dto);
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task InitialAssignAccount_Manager_Forbidden()
    {
        SetCurrentUser(2);
        var dto = new DeviceInitialAssignAccountItem { Name = "Init", AccountId = _account1.Id };
        var response = await _controller.InitialAssignAccount(2, dto);
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
    public async Task AssignGroup_NotFound_Returns404()
    {
        SetCurrentUser(1);
        var dto = new DeviceAssignGroupItem { DeviceGroupId = 1 };
        var response = await _controller.AssignGroup(999, dto);
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task InitialAssignAccount_NotFound_Returns404()
    {
        SetCurrentUser(1);
        var dto = new DeviceInitialAssignAccountItem { Name = "N", AccountId = _account1.Id };
        var response = await _controller.InitialAssignAccount(999, dto);
        Assert.That(response, Is.TypeOf<ObjectResult>());
        var obj = response as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }
}

