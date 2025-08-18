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
    public async Task GetAll_Engineer_ReturnsAllWithLimitedInfo()
    {
        SetCurrentUser(3); // Engineer
        var result = await _controller.GetAll();
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(2)); // Should see all accounts
        
        // Verify all accounts have Id and Name but empty UserIds
        foreach (var account in result.Value!)
        {
            Assert.That(account.Id, Is.GreaterThan(0)); // Should have valid Id
            Assert.That(account.Name, Is.Not.Null.And.Not.Empty); // Should have valid Name
            Assert.That(account.UserIds, Is.Empty); // Should have empty UserIds
        }
        
        // Verify specific accounts are present
        var accountIds = result.Value!.Select(a => a.Id).ToList();
        Assert.That(accountIds, Is.EquivalentTo(new[] { _account1.Id, _account2.Id }));
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
    public async Task AddAccount_Admin_Creates()
    {
        SetCurrentUser(1);
        var dto = new AccountCreateItem { Name = "NewAcc" };
        var result = await _controller.AddAccount(dto);
        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        Assert.That(_dbContext.Accounts.Count(), Is.EqualTo(3));
    }

    [Test]
    public async Task AddAccount_Manager_Forbidden()
    {
        SetCurrentUser(2);
        var dto = new AccountCreateItem { Name = "NewAcc" };
        var result = await _controller.AddAccount(dto);
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
    public async Task AddAccount_Admin_CreatesWithUserIds()
    {
        SetCurrentUser(1);
        var dto = new AccountCreateItem { Name = "NewAcc", UserIds = new() { 2, 3 } }; // Use existing manager and engineer IDs
        var result = await _controller.AddAccount(dto);
        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var reference = (result.Result as CreatedAtActionResult)!.Value as Reference;
        Assert.That(reference, Is.Not.Null);
        var accId = reference!.Id;
        // Only manager users should be linked
        var userAccounts = _dbContext.UserAccounts.Where(ua => ua.AccountId == accId).ToList();
        Assert.That(userAccounts.Select(ua => ua.UserId), Is.EquivalentTo(new[] { 2 })); // Only manager (ID 2) should be linked
    }

    [Test]
    public async Task UpdateAccount_Admin_UpdatesUserIds()
    {
        SetCurrentUser(1);
        // Add another manager user for this specific test
        var manager2 = new User
        {
            Id = 4,
            Email = "manager2@example.com",
            FirstName = "Manager2",
            LastName = "Test",
            Password = BCrypt.Net.BCrypt.HashPassword("pwd"),
            UserRoles = [ new UserRole { UserId = 4, RoleId = _managerRole.Id, Role = _managerRole } ]
        };
        _dbContext.Users.Add(manager2);
        await _dbContext.SaveChangesAsync();

        var updateItem = new AccountUpdateItem { Name = "Acc1", UserIds = new() { 4 } };
        var result = await _controller.UpdateAccount(_account1.Id, updateItem);
        Assert.That(result, Is.TypeOf<NoContentResult>());
        var userAccounts = _dbContext.UserAccounts.Where(ua => ua.AccountId == _account1.Id).ToList();
        Assert.That(userAccounts.Select(ua => ua.UserId), Is.EquivalentTo(new[] { 4 }));
    }

    [Test]
    public async Task UpdateAccount_Admin_RemovesUserAccountsIfNoManagers()
    {
        SetCurrentUser(1);
        // Update with only engineer user
        var updateItem = new AccountUpdateItem { Name = "Acc1", UserIds = new() { 3 } }; // Engineer ID
        var result = await _controller.UpdateAccount(_account1.Id, updateItem);
        Assert.That(result, Is.TypeOf<NoContentResult>());
        var userAccounts = _dbContext.UserAccounts.Where(ua => ua.AccountId == _account1.Id).ToList();
        Assert.That(userAccounts, Is.Empty);
    }

    #endregion

    #region New Tests for UserIds Handling in Get and Update

    [Test]
    public async Task GetAll_Admin_FillsUserIds()
    {
        SetCurrentUser(1); // Admin
        // Add another manager user and link to account2
        var manager2 = new User
        {
            Id = 5,
            Email = "manager3@example.com",
            FirstName = "Manager3",
            LastName = "Test",
            Password = BCrypt.Net.BCrypt.HashPassword("pwd"),
            UserRoles = [ new UserRole { UserId = 5, RoleId = _managerRole.Id, Role = _managerRole } ],
            UserAccounts = [ new UserAccount { UserId = 5, AccountId = _account2.Id, Account = _account2 } ]
        };
        _dbContext.Users.Add(manager2);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetAll();
        Assert.That(result.Value, Is.Not.Null);
        foreach (var acc in result.Value!)
        {
            if (acc.Id == _account1.Id)
                Assert.That(acc.UserIds, Is.EquivalentTo(new[] { 2 }));
            if (acc.Id == _account2.Id)
                Assert.That(acc.UserIds, Is.EquivalentTo(new[] { 5 }));
        }
    }

    [Test]
    public async Task GetAccount_Admin_FillsUserIds()
    {
        SetCurrentUser(1); // Admin
        var result = await _controller.GetAccount(_account1.Id);
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.UserIds, Is.EquivalentTo(new[] { 2 }));
    }

    [Test]
    public async Task UpdateAccount_Admin_UserIdsReflectedInGet()
    {
        SetCurrentUser(1); // Admin
        // Add another manager user for this specific test
        var manager2 = new User
        {
            Id = 6,
            Email = "manager4@example.com",
            FirstName = "Manager4",
            LastName = "Test",
            Password = BCrypt.Net.BCrypt.HashPassword("pwd"),
            UserRoles = [ new UserRole { UserId = 6, RoleId = _managerRole.Id, Role = _managerRole } ]
        };
        _dbContext.Users.Add(manager2);
        await _dbContext.SaveChangesAsync();
        // Update account1 to only have manager2
        var updateItem = new AccountUpdateItem { Name = "Acc1", UserIds = new() { 6 } };
        var updateResult = await _controller.UpdateAccount(_account1.Id, updateItem);
        Assert.That(updateResult, Is.TypeOf<NoContentResult>());
        // Now GetAccount should reflect new UserIds
        var getResult = await _controller.GetAccount(_account1.Id);
        Assert.That(getResult.Value, Is.Not.Null);
        Assert.That(getResult.Value!.UserIds, Is.EquivalentTo(new[] { 6 }));
    }

    #endregion
    
    #region GetAccountsByManager Tests

    [Test]
    public async Task GetAccountsByManager_ReturnsAccounts_WhenAdminAccesses()
    {
        // Arrange
        SetCurrentUser(1); // Admin user (reuse existing _admin)

        // Act - Use existing manager ID (2)
        var result = await _controller.GetAccountsByManager(2);

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        var accounts = result.Value!.ToList();
        Assert.That(accounts, Has.Count.EqualTo(1)); // _manager is linked to _account1
        Assert.That(accounts.Any(a => a.Id == 1 && a.Name == "Acc1"));
    }

    [Test]
    public async Task GetAccountsByManager_ReturnsAccounts_WhenUserAccessesOwnAccounts()
    {
        // Arrange
        SetCurrentUser(2); // Manager user accessing own accounts (reuse existing _manager)

        // Act
        var result = await _controller.GetAccountsByManager(2);

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        var accounts = result.Value!.ToList();
        Assert.That(accounts, Has.Count.EqualTo(1));
        Assert.That(accounts[0].Id, Is.EqualTo(1)); // _account1.Id
        Assert.That(accounts[0].Name, Is.EqualTo("Acc1"));
    }

    [Test]
    public async Task GetAccountsByManager_ReturnsForbidden_WhenNonAdminAccessesOtherUser()
    {
        // Arrange
        SetCurrentUser(2); // Manager user (reuse existing _manager)

        // Act - Try to access another manager's accounts (use engineer ID)
        var result = await _controller.GetAccountsByManager(3);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetAccountsByManager_ReturnsNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        SetCurrentUser(1); // Admin user (reuse existing _admin)

        // Act
        var result = await _controller.GetAccountsByManager(999);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetAccountsByManager_ReturnsEmptyArray_WhenUserIsNotManager()
    {
        // Arrange
        SetCurrentUser(1); // Admin user (reuse existing _admin)

        // Act - Use existing engineer ID (3)
        var result = await _controller.GetAccountsByManager(3);

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        var accounts = result.Value!.ToList();
        Assert.That(accounts, Is.Empty);
    }

    [Test]
    public async Task GetAccountsByManager_ReturnsEmptyArray_WhenManagerHasNoAccounts()
    {
        // Arrange
        SetCurrentUser(1); // Admin user (reuse existing _admin)
        
        // Create a manager without accounts only for this specific test
        var managerWithoutAccounts = new User
        {
            Id = 7,
            FirstName = "Manager",
            LastName = "NoAccounts",
            Patronymic = "",
            Email = "manager.noaccounts@example.com",
            Password = BCrypt.Net.BCrypt.HashPassword("password"),
            UserRoles = [new UserRole { UserId = 7, RoleId = _managerRole.Id, Role = _managerRole }],
            UserAccounts = [] // No accounts assigned
        };

        _dbContext.Users.Add(managerWithoutAccounts);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetAccountsByManager(7);

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        var accounts = result.Value!.ToList();
        Assert.That(accounts, Is.Empty);
    }

    [Test]
    public async Task GetAccountsByManager_ReturnsForbidden_WhenNoCurrentUser()
    {
        // Arrange
        SetCurrentUser(0); // No user (simulate no authentication)

        // Act - Use existing manager ID (2)
        var result = await _controller.GetAccountsByManager(2);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetAccountsByManager_WorksCorrectly_WhenManagerAccessesOwnAccountsWithMultipleRoles()
    {
        // Arrange - Create a user with multiple roles including manager for this specific test
        var multiRoleManager = new User
        {
            Id = 8,
            FirstName = "MultiRole",
            LastName = "Manager",
            Patronymic = "",
            Email = "multirole@example.com",
            Password = BCrypt.Net.BCrypt.HashPassword("password"),
            UserRoles = [
                new UserRole { UserId = 8, RoleId = _engineerRole.Id, Role = _engineerRole },
                new UserRole { UserId = 8, RoleId = _managerRole.Id, Role = _managerRole }
            ],
            UserAccounts = [
                new UserAccount { UserId = 8, AccountId = _account1.Id, Account = _account1 },
                new UserAccount { UserId = 8, AccountId = _account2.Id, Account = _account2 }
            ]
        };

        _dbContext.Users.Add(multiRoleManager);
        await _dbContext.SaveChangesAsync();
        
        SetCurrentUser(8); // MultiRole Manager user

        // Act
        var result = await _controller.GetAccountsByManager(8);

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        var accounts = result.Value!.ToList();
        Assert.That(accounts, Has.Count.EqualTo(2));
        Assert.That(accounts.Any(a => a.Id == 1 && a.Name == "Acc1"));
        Assert.That(accounts.Any(a => a.Id == 2 && a.Name == "Acc2"));
    }

    [Test]
    public async Task GetAccountsByManager_ReturnsEmptyArray_WhenUserHasMultipleRolesButNotManager()
    {
        // Arrange
        SetCurrentUser(1); // Admin user (reuse existing _admin)
        
        // Create a user with multiple roles but not manager for this specific test
        var userWithoutManagerRole = new User
        {
            Id = 9,
            FirstName = "NonManager",
            LastName = "User",
            Patronymic = "",
            Email = "nonmanager@example.com",
            Password = BCrypt.Net.BCrypt.HashPassword("password"),
            UserRoles = [
                new UserRole { UserId = 9, RoleId = _adminRole.Id, Role = _adminRole },
                new UserRole { UserId = 9, RoleId = _engineerRole.Id, Role = _engineerRole }
            ],
            UserAccounts = [new UserAccount { UserId = 9, AccountId = _account1.Id, Account = _account1 }]
        };

        _dbContext.Users.Add(userWithoutManagerRole);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetAccountsByManager(9);

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        var accounts = result.Value!.ToList();
        Assert.That(accounts, Is.Empty);
    }

    #endregion
}

