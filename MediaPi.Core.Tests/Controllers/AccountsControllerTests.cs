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
using System.Threading.Tasks;
using MediaPi.Core.Controllers;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services;

namespace MediaPi.Core.Tests.Controllers;

[TestFixture]
public class AccountsControllerTests
{
#pragma warning disable CS8618
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private Mock<ILogger<AccountsController>> _mockLogger;
    private AppDbContext _dbContext;
    private AccountsController _controller;
    private User _admin;
    private User _manager;
    private User _engineer;
    private Role _adminRole;
    private Role _managerRole;
    private Role _engineerRole;
    private Account _account1;
    private Account _account2;
    private DeviceGroup _group1;
    private Device _device1;
    private UserInformationService _userInformationService;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"accounts_controller_test_db_{System.Guid.NewGuid()}")
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
        _dbContext.DeviceGroups.Add(_group1);

        _device1 = new Device { Id = 1, Name = "Dev1", IpAddress = "1.1.1.1", AccountId = _account1.Id, DeviceGroupId = _group1.Id };
        _dbContext.Devices.Add(_device1);

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
            Email = "engineer@example.com",
            Password = pass,
            UserRoles = [ new UserRole { UserId = 3, RoleId = _engineerRole.Id, Role = _engineerRole } ]
        };

        _dbContext.Users.AddRange(_admin, _manager, _engineer);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockLogger = new Mock<ILogger<AccountsController>>();
        _userInformationService = new UserInformationService(_dbContext);
    }

    private void SetCurrentUser(int? id)
    {
        var context = new DefaultHttpContext();
        if (id.HasValue) context.Items["UserId"] = id.Value;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);
        _controller = new AccountsController(
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
        Assert.That(result.Value!.First().Id, Is.EqualTo(_account1.Id));
    }

    [Test]
    public async Task GetAccount_Manager_Other_Forbidden()
    {
        SetCurrentUser(2);
        var result = await _controller.GetAccount(_account2.Id);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task PostAccount_Admin_Creates()
    {
        SetCurrentUser(1);
        var dto = new AccountCreateItem { Name = "NewAcc" };
        var result = await _controller.PostAccount(dto);
        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        Assert.That(_dbContext.Accounts.Count(), Is.EqualTo(3));
    }

    [Test]
    public async Task PostAccount_Manager_Forbidden()
    {
        SetCurrentUser(2);
        var dto = new AccountCreateItem { Name = "NewAcc" };
        var result = await _controller.PostAccount(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DeleteAccount_Admin_SetsDeviceNull()
    {
        SetCurrentUser(1);
        var result = await _controller.DeleteAccount(_account1.Id);
        Assert.That(result, Is.TypeOf<NoContentResult>());
        var dev = await _dbContext.Devices.FindAsync(_device1.Id);
        Assert.That(dev!.AccountId, Is.Null);
        Assert.That(dev.DeviceGroupId, Is.Null);
        var grp = await _dbContext.DeviceGroups.FindAsync(_group1.Id);
        Assert.That(grp, Is.Null);
        var acc = await _dbContext.Accounts.FindAsync(_account1.Id);
        Assert.That(acc, Is.Null);
    }

    #region UpdateAccount Tests

    [Test]
    public async Task UpdateAccount_Admin_UpdatesAccount()
    {
        // Arrange
        SetCurrentUser(1);
        var updateItem = new AccountUpdateItem { Name = "UpdatedAccount" };

        // Act
        var result = await _controller.UpdateAccount(_account1.Id, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<NoContentResult>());
        var updatedAccount = await _dbContext.Accounts.FindAsync(_account1.Id);
        Assert.That(updatedAccount, Is.Not.Null);
        Assert.That(updatedAccount!.Name, Is.EqualTo("UpdatedAccount"));
    }

    [Test]
    public async Task UpdateAccount_NoUser_Returns403()
    {
        // Arrange
        SetCurrentUser(null);
        var updateItem = new AccountUpdateItem { Name = "UpdatedAccount" };

        // Act
        var result = await _controller.UpdateAccount(_account1.Id, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objectResult = result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task UpdateAccount_NotAdmin_Returns403()
    {
        // Arrange
        SetCurrentUser(2); // Manager
        var updateItem = new AccountUpdateItem { Name = "UpdatedAccount" };

        // Act
        var result = await _controller.UpdateAccount(_account1.Id, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objectResult = result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        
        // Verify no change occurred
        var account = await _dbContext.Accounts.FindAsync(_account1.Id);
        Assert.That(account!.Name, Is.EqualTo("Acc1"));
    }

    [Test]
    public async Task UpdateAccount_Engineer_Returns403()
    {
        // Arrange
        SetCurrentUser(3); // Engineer
        var updateItem = new AccountUpdateItem { Name = "UpdatedAccount" };

        // Act
        var result = await _controller.UpdateAccount(_account1.Id, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objectResult = result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task UpdateAccount_NonExistentAccount_Returns404()
    {
        // Arrange
        SetCurrentUser(1); // Admin
        var updateItem = new AccountUpdateItem { Name = "UpdatedAccount" };
        int nonExistentAccountId = 999;

        // Act
        var result = await _controller.UpdateAccount(nonExistentAccountId, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objectResult = result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task UpdateAccount_DuplicateName_Returns409()
    {
        // Arrange
        SetCurrentUser(1); // Admin
        var updateItem = new AccountUpdateItem { Name = "Acc2" }; // Try to set account1's name to account2's name

        // Act
        var result = await _controller.UpdateAccount(_account1.Id, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objectResult = result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        
        // Verify no change occurred
        var account = await _dbContext.Accounts.FindAsync(_account1.Id);
        Assert.That(account!.Name, Is.EqualTo("Acc1"));
    }

    [Test]
    public async Task UpdateAccount_SameName_Succeeds()
    {
        // Arrange
        SetCurrentUser(1); // Admin
        var updateItem = new AccountUpdateItem { Name = "Acc1" }; // Same name should not cause conflict

        // Act
        var result = await _controller.UpdateAccount(_account1.Id, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<NoContentResult>());
        var account = await _dbContext.Accounts.FindAsync(_account1.Id);
        Assert.That(account!.Name, Is.EqualTo("Acc1"));
    }

    [Test]
    public async Task UpdateAccount_NullName_NoChanges()
    {
        // Arrange
        SetCurrentUser(1); // Admin
        var updateItem = new AccountUpdateItem { Name = null }; // Null values should not change existing data

        // Act
        var result = await _controller.UpdateAccount(_account1.Id, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<NoContentResult>());
        var account = await _dbContext.Accounts.FindAsync(_account1.Id);
        Assert.That(account!.Name, Is.EqualTo("Acc1")); // Name remains unchanged
    }

    #endregion

    #region Additional Tests for UserIds Handling

    [Test]
    public async Task PostAccount_Admin_CreatesWithUserIds()
    {
        SetCurrentUser(1);
        // Add another manager user
        var manager2 = new User
        {
            Id = 4,
            Email = "manager2@example.com",
            Password = BCrypt.Net.BCrypt.HashPassword("pwd"),
            UserRoles = [ new UserRole { UserId = 4, RoleId = _managerRole.Id, Role = _managerRole } ]
        };
        _dbContext.Users.Add(manager2);
        await _dbContext.SaveChangesAsync();

        var dto = new AccountCreateItem { Name = "NewAcc", UserIds = new() { 2, 4, 3 } };
        var result = await _controller.PostAccount(dto);
        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var reference = (result.Result as CreatedAtActionResult)!.Value as Reference;
        Assert.That(reference, Is.Not.Null);
        var accId = reference!.Id;
        // Only manager users should be linked
        var userAccounts = _dbContext.UserAccounts.Where(ua => ua.AccountId == accId).ToList();
        Assert.That(userAccounts.Select(ua => ua.UserId), Is.EquivalentTo(new[] { 2, 4 }));
    }

    [Test]
    public async Task UpdateAccount_Admin_UpdatesUserIds()
    {
        SetCurrentUser(1);
        // Add another manager user
        var manager2 = new User
        {
            Id = 5,
            Email = "manager5@example.com",
            Password = BCrypt.Net.BCrypt.HashPassword("pwd"),
            UserRoles = [ new UserRole { UserId = 5, RoleId = _managerRole.Id, Role = _managerRole } ]
        };
        _dbContext.Users.Add(manager2);
        await _dbContext.SaveChangesAsync();
        // The UserAccount for user 2 and account1 is already present via _manager.UserAccounts in Setup
        // No need to add it again here

        var updateItem = new AccountUpdateItem { Name = "Acc1", UserIds = new() { 5 } };
        var result = await _controller.UpdateAccount(_account1.Id, updateItem);
        Assert.That(result, Is.TypeOf<NoContentResult>());
        var userAccounts = _dbContext.UserAccounts.Where(ua => ua.AccountId == _account1.Id).ToList();
        Assert.That(userAccounts.Select(ua => ua.UserId), Is.EquivalentTo(new[] { 5 }));
    }

    [Test]
    public async Task UpdateAccount_Admin_RemovesUserAccountsIfNoManagers()
    {
        SetCurrentUser(1);
        // The UserAccount for user 2 and account1 is already present via _manager.UserAccounts in Setup
        // No need to add it again here
        // Update with only engineer user
        var updateItem = new AccountUpdateItem { Name = "Acc1", UserIds = new() { 3 } };
        var result = await _controller.UpdateAccount(_account1.Id, updateItem);
        Assert.That(result, Is.TypeOf<NoContentResult>());
        var userAccounts = _dbContext.UserAccounts.Where(ua => ua.AccountId == _account1.Id).ToList();
        Assert.That(userAccounts, Is.Empty);
    }

    #endregion
}

