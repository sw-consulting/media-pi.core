// Developed by Maxim [maxirmx] Samsonov (www.sw.consulting)
// This file is a part of Media Pi backend application

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

using MediaPi.Core.Controllers;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services;

namespace MediaPi.Core.Tests.Controllers;

[TestFixture]
public class UsersControllerTests
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. 
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private Mock<ILogger<UsersController>> _mockLogger;
    private Mock<IUserInformationService> _mockUserInformationService;
    private AppDbContext _dbContext;
    private UsersController _controller;
    private User _adminUser;
    private User _operatorUser;
    private User _customerUser;
    private Role _adminRole;
    private Role _operatorRole;
    private Role _customerRole;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor.

    [SetUp]
    public void Setup()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"users_controller_test_db_{System.Guid.NewGuid()}")
            .Options;

        _dbContext = new AppDbContext(options);

        // Add roles - only set RoleId, let database handle Id
        _adminRole = new Role { RoleId = UserRoleConstants.SystemAdministrator, Name = "Администратор" };
        _operatorRole = new Role { RoleId = UserRoleConstants.AccountManager, Name = "Менеджер" };
        _customerRole = new Role { RoleId = UserRoleConstants.InstallationEngineer, Name = "Инженер-установшик" };
        _dbContext.Roles.AddRange(_adminRole, _operatorRole, _customerRole);
        _dbContext.SaveChanges(); // Save roles first to get their generated Ids

        // Setup users with hashed passwords
        string hashedPassword = BCrypt.Net.BCrypt.HashPassword("password123");

        _adminUser = new User
        {
            Id = 1,
            Email = "admin@example.com",
            Password = hashedPassword,
            FirstName = "Admin",
            LastName = "User",
            Patronymic = "",
            UserRoles =
            [
                new UserRole { UserId = 1, RoleId = _adminRole.Id, Role = _adminRole }
            ]
        };

        _operatorUser = new User
        {
            Id = 2,
            Email = "operator@example.com",
            Password = hashedPassword,
            FirstName = "Operator",
            LastName = "User",
            Patronymic = "",
            UserRoles =
            [
                new UserRole { UserId = 2, RoleId = _operatorRole.Id, Role = _operatorRole }
            ]
        };

        _customerUser = new User
        {
            Id = 3,
            Email = "customer@example.com",
            Password = hashedPassword,
            FirstName = "Customer",
            LastName = "User",
            Patronymic = "",
            UserRoles =
            [
                new UserRole { UserId = 3, RoleId = _customerRole.Id, Role = _customerRole }
            ]
        };

        // Setup mocks
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockLogger = new Mock<ILogger<UsersController>>();
        _mockUserInformationService = new Mock<IUserInformationService>();

        // Save entities to database
        _dbContext.SaveChanges();
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region GetUsers Tests

    [Test]
    public async Task GetUsers_ReturnsAllUsers_WhenUserIsAdmin()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.AddRange(_adminUser, _operatorUser);
        await _dbContext.SaveChangesAsync();

        var expectedUsers = new List<UserViewItem>
        {
            new(_adminUser),
            new(_operatorUser)
        };

        _mockUserInformationService.Setup(x => x.CheckAdmin(1)).ReturnsAsync(true);
        _mockUserInformationService.Setup(x => x.UserViewItems()).ReturnsAsync(expectedUsers);

        // Act
        var result = await _controller.GetUsers();

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value, Is.InstanceOf<IEnumerable<UserViewItem>>());
        var users = result.Value as IEnumerable<UserViewItem>;
        Assert.That(users, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetUsers_ReturnsForbidden_WhenUserIsNotAdmin()
    {
        // Arrange
        SetCurrentUserId(2); // Operator user
        _dbContext.Users.AddRange(_adminUser, _operatorUser);
        await _dbContext.SaveChangesAsync();

        _mockUserInformationService.Setup(x => x.CheckAdmin(2)).ReturnsAsync(false);

        // Act
        var result = await _controller.GetUsers();

        // Assert
        Assert.That(result.Result, Is.Not.Null);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    #endregion

    #region GetUser Tests

    [Test]
    public async Task GetUser_ReturnsUser_WhenUserIsAdmin()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.AddRange(_adminUser, _operatorUser, _customerUser);
        await _dbContext.SaveChangesAsync();

        var expectedUser = new UserViewItem(_customerUser);

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(3, 1)).ReturnsAsync(true);
        _mockUserInformationService.Setup(x => x.UserViewItem(3)).ReturnsAsync(expectedUser);

        // Act
        var result = await _controller.GetUser(3); // Getting customer user

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value, Is.TypeOf<UserViewItem>());
        var user = result.Value;
        Assert.That(user!.Id, Is.EqualTo(3));
        Assert.That(user.Email, Is.EqualTo("customer@example.com"));
    }

    [Test]
    public async Task GetUser_ReturnsUser_WhenUserAccessesSelf()
    {
        // Arrange
        SetCurrentUserId(2); // Operator user
        _dbContext.Users.AddRange(_adminUser, _operatorUser, _customerUser);
        await _dbContext.SaveChangesAsync();

        var expectedUser = new UserViewItem(_operatorUser);

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(2, 2)).ReturnsAsync(true);
        _mockUserInformationService.Setup(x => x.UserViewItem(2)).ReturnsAsync(expectedUser);

        // Act
        var result = await _controller.GetUser(2); // Getting self

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value, Is.TypeOf<UserViewItem>());
        var user = result.Value;
        Assert.That(user!.Id, Is.EqualTo(2));
        Assert.That(user.Email, Is.EqualTo("operator@example.com"));
    }

    [Test]
    public async Task GetUser_ReturnsForbidden_WhenNonAdminAccessesOtherUser()
    {
        // Arrange
        SetCurrentUserId(2); // Operator user
        _dbContext.Users.AddRange(_adminUser, _operatorUser, _customerUser);
        await _dbContext.SaveChangesAsync();

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(1, 2)).ReturnsAsync(false);

        // Act
        var result = await _controller.GetUser(1); // Getting admin user

        // Assert
        Assert.That(result.Result, Is.Not.Null);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetUser_ReturnsNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.AddRange(_adminUser, _operatorUser, _customerUser);
        await _dbContext.SaveChangesAsync();

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(999, 1)).ReturnsAsync(true);
        _mockUserInformationService.Setup(x => x.UserViewItem(999)).ReturnsAsync((UserViewItem?)null);

        // Act
        var result = await _controller.GetUser(999); // Non-existent user

        // Assert
        Assert.That(result.Result, Is.Not.Null);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    #endregion

    #region AddUser Tests

    [Test]
    public async Task PostUser_CreatesUser_WhenUserIsAdmin()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.Add(_adminUser);
        await _dbContext.SaveChangesAsync();

        var newUser = new UserCreateItem
        {
            Email = "new@example.com",
            Password = "newpassword",
            FirstName = "New",
            LastName = "User",
            Patronymic = "",
            Roles = [ UserRoleConstants.InstallationEngineer ] 
        };

        _mockUserInformationService.Setup(x => x.CheckAdmin(1)).ReturnsAsync(true);
        _mockUserInformationService.Setup(x => x.Exists("new@example.com")).Returns(false);

        // Act
        var result = await _controller.AddUser(newUser);

        // Assert
        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var createdAtActionResult = result.Result as CreatedAtActionResult;
        Assert.That(createdAtActionResult!.ActionName, Is.EqualTo(nameof(UsersController.AddUser)));
        Assert.That(createdAtActionResult.Value, Is.TypeOf<Reference>());

        var reference = createdAtActionResult.Value as Reference;
        Assert.That(reference!.Id, Is.GreaterThan(0));

        // Verify user was added to database
        var savedUser = await _dbContext.Users.FindAsync(reference.Id);
        Assert.That(savedUser, Is.Not.Null);
        Assert.That(savedUser!.Email, Is.EqualTo("new@example.com"));
        // Check that password was hashed
        Assert.That(BCrypt.Net.BCrypt.Verify("newpassword", savedUser.Password), Is.True);
        Assert.That(savedUser.UserRoles, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task PostUser_ReturnsForbidden_WhenUserIsNotAdmin()
    {
        // Arrange
        SetCurrentUserId(2); // Operator user
        _dbContext.Users.AddRange(_adminUser, _operatorUser, _customerUser);
        await _dbContext.SaveChangesAsync();

        var newUser = new UserCreateItem
        {
            Email = "new@example.com",
            Password = "newpassword",
            FirstName = "New",
            LastName = "User",
            Patronymic = ""
        };

        _mockUserInformationService.Setup(x => x.CheckAdmin(2)).ReturnsAsync(false);

        // Act
        var result = await _controller.AddUser(newUser);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task PostUser_ReturnsConflict_WhenEmailAlreadyExists()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.AddRange(_adminUser, _operatorUser, _customerUser);
        await _dbContext.SaveChangesAsync();

        var newUser = new UserCreateItem
        {
            Email = "customer@example.com", // Already exists
            Password = "newpassword",
            FirstName = "New",
            LastName = "User",
            Patronymic = ""
        };

        _mockUserInformationService.Setup(x => x.CheckAdmin(1)).ReturnsAsync(true);
        _mockUserInformationService.Setup(x => x.Exists("customer@example.com")).Returns(true);

        // Act
        var result = await _controller.AddUser(newUser);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task PostUser_CreatesAccountManagerWithAccounts()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.Add(_adminUser);
        _dbContext.Accounts.Add(new Account { Id = 10, Name = "Acc10" });
        _dbContext.Accounts.Add(new Account { Id = 11, Name = "Acc11" });
        await _dbContext.SaveChangesAsync();

        var newUser = new UserCreateItem
        {
            Email = "manager@example.com",
            Password = "newpassword",
            FirstName = "Manager",
            LastName = "User",
            Patronymic = "",
            Roles = [ UserRoleConstants.AccountManager ],
            AccountIds = new List<int> { 10, 11 }
        };

        _mockUserInformationService.Setup(x => x.CheckAdmin(1)).ReturnsAsync(true);
        _mockUserInformationService.Setup(x => x.Exists("manager@example.com")).Returns(false);

        // Act
        var result = await _controller.AddUser(newUser);

        // Assert
        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var createdAtActionResult = result.Result as CreatedAtActionResult;
        var reference = createdAtActionResult!.Value as Reference;
        Assert.That(reference!.Id, Is.GreaterThan(0));

        // Verify user was added to database
        var savedUser = await _dbContext.Users.Include(u => u.UserAccounts).FirstOrDefaultAsync(u => u.Id == reference.Id);
        Assert.That(savedUser, Is.Not.Null);
        Assert.That(savedUser!.UserAccounts.Select(ua => ua.AccountId), Is.EquivalentTo(new[] { 10, 11 }));
    }

    #endregion

    #region ChangeUser Tests

    [Test]
    public async Task PutUser_UpdatesUser_WhenUserIsAdmin()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.AddRange(_adminUser, _operatorUser);
        await _dbContext.SaveChangesAsync();

        var updateItem = new UserUpdateItem
        {
            FirstName = "Updated",
            LastName = "Name",
            Email = "updated@example.com",
            Roles = [UserRoleConstants.InstallationEngineer]
        };

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(2, 1)).ReturnsAsync(true);
        _mockUserInformationService.Setup(x => x.Exists("updated@example.com")).Returns(false);

        // Act
        var result = await _controller.ChangeUser(2, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<NoContentResult>());

        // Verify user was updated
        var updatedUser = await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == 2);

        Assert.That(updatedUser, Is.Not.Null);
        Assert.That(updatedUser!.FirstName, Is.EqualTo("Updated"));
        Assert.That(updatedUser.LastName, Is.EqualTo("Name"));
        Assert.That(updatedUser.Email, Is.EqualTo("updated@example.com"));
        Assert.That(updatedUser.UserRoles, Has.Count.EqualTo(1));
        Assert.That(updatedUser.UserRoles.First().Role!.RoleId, Is.EqualTo(UserRoleConstants.InstallationEngineer));
    }

    [Test]
    public async Task PutUser_UpdatesOwnData_WhenNotAdmin()
    {
        // Arrange
        SetCurrentUserId(2); // Operator user
        _dbContext.Users.AddRange(_adminUser, _operatorUser);
        await _dbContext.SaveChangesAsync();

        var updateItem = new UserUpdateItem
        {
            FirstName = "Self",
            LastName = "Updated",
            // Not changing roles as non-admin
        };

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(2, 2)).ReturnsAsync(true);

        // Act
        var result = await _controller.ChangeUser(2, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<NoContentResult>());

        // Verify user was updated
        var updatedUser = await _dbContext.Users.FindAsync(2);
        Assert.That(updatedUser, Is.Not.Null);
        Assert.That(updatedUser!.FirstName, Is.EqualTo("Self"));
        Assert.That(updatedUser.LastName, Is.EqualTo("Updated"));
    }

    [Test]
    public async Task PutUser_ReturnsForbidden_WhenNonAdminUpdatesOtherUser()
    {
        // Arrange
        SetCurrentUserId(2); // Operator user
        _dbContext.Users.AddRange(_adminUser, _operatorUser);
        await _dbContext.SaveChangesAsync();

        var updateItem = new UserUpdateItem
        {
            FirstName = "Try",
            LastName = "Update"
        };

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(1, 2)).ReturnsAsync(false);

        // Act
        var result = await _controller.ChangeUser(1, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objectResult = result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task PutUser_ReturnsNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.AddRange(_adminUser, _operatorUser);
        await _dbContext.SaveChangesAsync();

        var updateItem = new UserUpdateItem
        {
            FirstName = "Not",
            LastName = "Found"
        };

        // Act
        var result = await _controller.ChangeUser(999, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objectResult = result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task PutUser_ReturnsConflict_WhenEmailAlreadyExists()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.AddRange(_adminUser, _operatorUser);
        await _dbContext.SaveChangesAsync();

        var updateItem = new UserUpdateItem
        {
            Email = "admin@example.com" // Already exists
        };

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(2, 1)).ReturnsAsync(true);
        _mockUserInformationService.Setup(x => x.Exists("admin@example.com")).Returns(true);

        // Act
        var result = await _controller.ChangeUser(2, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objectResult = result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task PutUser_UpdatesPassword_WhenProvided()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.AddRange(_adminUser, _operatorUser);
        await _dbContext.SaveChangesAsync();

        var updateItem = new UserUpdateItem
        {
            Password = "newpassword123"
        };

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(2, 1)).ReturnsAsync(true);

        // Act
        var result = await _controller.ChangeUser(2, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<NoContentResult>());

        // Verify password was updated and hashed
        var updatedUser = await _dbContext.Users.FindAsync(2);
        Assert.That(updatedUser, Is.Not.Null);
        Assert.That(BCrypt.Net.BCrypt.Verify("newpassword123", updatedUser!.Password), Is.True);
    }

    [Test]
    public async Task PutUser_RequiresAdmin_WhenChangingAdminRole()
    {
        // Arrange
        SetCurrentUserId(2); // Operator user
        _dbContext.Users.AddRange(_adminUser, _operatorUser);
        await _dbContext.SaveChangesAsync();

        var updateItem = new UserUpdateItem
        {
            FirstName = "Try",
            Roles = [UserRoleConstants.SystemAdministrator] // Trying to become admin
        };

        _mockUserInformationService.Setup(x => x.CheckAdmin(2)).ReturnsAsync(false);

        // Act
        var result = await _controller.ChangeUser(2, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objectResult = result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task PutUser_EmptyRoles_RemovesAllUserRoles()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.Add(_operatorUser);
        await _dbContext.SaveChangesAsync();

        var userRoles = _dbContext.UserRoles.Where(ur => ur.UserId == 2).ToList();
        Assert.That(userRoles, Is.Not.Empty);

        var updateItem = new UserUpdateItem
        {
            Roles = []
        };

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(2, 1)).ReturnsAsync(true);
        // Act
        var result = await _controller.ChangeUser(2, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<NoContentResult>());
        userRoles = _dbContext.UserRoles.Where(ur => ur.UserId == 2).ToList();
        Assert.That(userRoles, Is.Empty);
    }

    [Test]
    public async Task PutUser_AccountManager_UpdatesAccountIds()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.Add(_operatorUser);
        _dbContext.Accounts.Add(new Account { Id = 20, Name = "Acc20" });
        _dbContext.Accounts.Add(new Account { Id = 21, Name = "Acc21" });
        await _dbContext.SaveChangesAsync();
        // Add initial UserAccount
        _dbContext.UserAccounts.Add(new UserAccount { UserId = 2, AccountId = 20 });
        await _dbContext.SaveChangesAsync();

        var updateItem = new UserUpdateItem
        {
            Roles = [ UserRoleConstants.AccountManager ],
            AccountIds = new List<int> { 21 }
        };

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(2, 1)).ReturnsAsync(true);

        // Act
        var result = await _controller.ChangeUser(2, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<NoContentResult>());
        var updatedUser = await _dbContext.Users.Include(u => u.UserAccounts).FirstOrDefaultAsync(u => u.Id == 2);
        Assert.That(updatedUser, Is.Not.Null);
        Assert.That(updatedUser!.UserAccounts.Select(ua => ua.AccountId), Is.EquivalentTo(new[] { 21 }));
    }

    [Test]
    public async Task PutUser_RemovesAccounts_WhenNotAccountManager()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.Add(_operatorUser);
        _dbContext.Accounts.Add(new Account { Id = 30, Name = "Acc30" });
        await _dbContext.SaveChangesAsync();
        _dbContext.UserAccounts.Add(new UserAccount { UserId = 2, AccountId = 30 });
        await _dbContext.SaveChangesAsync();

        var updateItem = new UserUpdateItem
        {
            Roles = [ UserRoleConstants.InstallationEngineer ],
            AccountIds = new List<int> { 30 } // Should be ignored
        };

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(2, 1)).ReturnsAsync(true);

        // Act
        var result = await _controller.ChangeUser(2, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<NoContentResult>());
        var updatedUser = await _dbContext.Users.Include(u => u.UserAccounts).FirstOrDefaultAsync(u => u.Id == 2);
        Assert.That(updatedUser, Is.Not.Null);
        Assert.That(updatedUser!.UserAccounts, Is.Empty);
    }

    [Test]
    public async Task PutUser_RolesNull_DoesNotChangeUserRoles()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.Add(_customerUser);
        await _dbContext.SaveChangesAsync();

        var updateItem = new UserUpdateItem
        {
            Roles = null, // Explicitly null
            FirstName = "UpdatedName"
        };

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(_customerUser.Id, 1)).ReturnsAsync(true);
        // Act
        var result = await _controller.ChangeUser(_customerUser.Id, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<NoContentResult>());
        var updatedUser = await _dbContext.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Id == _customerUser.Id);
        Assert.That(updatedUser, Is.Not.Null);
        // Roles should remain unchanged
        Assert.That(updatedUser!.UserRoles.Count, Is.EqualTo(1));
        Assert.That(updatedUser.UserRoles.First().RoleId, Is.EqualTo(_customerRole.Id));
        Assert.That(updatedUser.FirstName, Is.EqualTo("UpdatedName"));
    }

    #endregion

    #region DeleteUser Tests

    [Test]
    public async Task DeleteUser_DeletesUser_WhenUserIsAdmin()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.AddRange(_adminUser, _operatorUser, _customerUser);
        await _dbContext.SaveChangesAsync();

        _mockUserInformationService.Setup(x => x.CheckAdmin(1)).ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteUser(2);

        // Assert
        Assert.That(result, Is.TypeOf<NoContentResult>());

        // Verify user was deleted
        var deletedUser = await _dbContext.Users.FindAsync(2);
        Assert.That(deletedUser, Is.Null);
    }

    [Test]
    public async Task DeleteUser_ReturnsForbidden_WhenUserIsNotAdmin()
    {
        // Arrange
        SetCurrentUserId(2); // Operator user
        _dbContext.Users.AddRange(_adminUser, _operatorUser);
        await _dbContext.SaveChangesAsync();

        _mockUserInformationService.Setup(x => x.CheckAdmin(2)).ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteUser(1);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objectResult = result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DeleteUser_ReturnsNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.AddRange(_adminUser, _operatorUser);
        await _dbContext.SaveChangesAsync();

        _mockUserInformationService.Setup(x => x.CheckAdmin(1)).ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteUser(999);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objectResult = result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    #endregion

    #region GetUsersByAccount Tests

    [Test]
    public async Task GetUsersByAccount_ReturnsManagers_WhenManagerAccessesOwnAccount()
    {
        // Arrange
        SetCurrentUserId(2); // Manager user
        var account = new Account { Id = 100, Name = "TestAccount" };
        var manager1 = new User
        {
            Id = 4,
            FirstName = "Manager1",
            LastName = "User",
            Patronymic = "Patronymic1",
            Email = "manager1@example.com",
            Password = BCrypt.Net.BCrypt.HashPassword("password"),
            UserRoles = [new UserRole { UserId = 4, RoleId = _operatorRole.Id, Role = _operatorRole }],
            UserAccounts = [new UserAccount { UserId = 4, AccountId = 100, Account = account }]
        };
        var manager2 = new User
        {
            Id = 5,
            FirstName = "Manager2",
            LastName = "User",
            Patronymic = "Patronymic2",
            Email = "manager2@example.com",
            Password = BCrypt.Net.BCrypt.HashPassword("password"),
            UserRoles = [new UserRole { UserId = 5, RoleId = _operatorRole.Id, Role = _operatorRole }],
            UserAccounts = [new UserAccount { UserId = 5, AccountId = 100, Account = account }]
        };

        _dbContext.Accounts.Add(account);
        _dbContext.Users.AddRange(_operatorUser, manager1, manager2);
        await _dbContext.SaveChangesAsync();

        _mockUserInformationService.Setup(x => x.CheckManager(2, 100)).ReturnsAsync(true);

        // Act
        var result = await _controller.GetUsersByAccount(100);

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        var managers = result.Value!.ToList();
        Assert.That(managers, Has.Count.EqualTo(2));
        Assert.That(managers.All(m => m.Email == ""));
        Assert.That(managers.All(m => m.Roles.Count == 0));
        Assert.That(managers.All(m => m.AccountIds.Count == 0));
        Assert.That(managers.Any(m => m.FirstName == "Manager1" && m.Patronymic == "Patronymic1"));
        Assert.That(managers.Any(m => m.FirstName == "Manager2" && m.Patronymic == "Patronymic2"));
    }

    [Test]
    public async Task GetUsersByAccount_ReturnsManagers_WhenAdminAccesses()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        var account = new Account { Id = 101, Name = "TestAccount2" };
        var manager = new User
        {
            Id = 6,
            FirstName = "Manager3",
            LastName = "User",
            Patronymic = "Patronymic3",
            Email = "manager3@example.com",
            Password = BCrypt.Net.BCrypt.HashPassword("password"),
            UserRoles = [new UserRole { UserId = 6, RoleId = _operatorRole.Id, Role = _operatorRole }],
            UserAccounts = [new UserAccount { UserId = 6, AccountId = 101, Account = account }]
        };

        _dbContext.Accounts.Add(account);
        _dbContext.Users.AddRange(_adminUser, manager);
        await _dbContext.SaveChangesAsync();

        _mockUserInformationService.Setup(x => x.CheckManager(1, 101)).ReturnsAsync(true);

        // Act
        var result = await _controller.GetUsersByAccount(101);

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        var managers = result.Value!.ToList();
        Assert.That(managers, Has.Count.EqualTo(1));
        Assert.That(managers[0].FirstName, Is.EqualTo("Manager3"));
        Assert.That(managers[0].LastName, Is.EqualTo("User"));
        Assert.That(managers[0].Patronymic, Is.EqualTo("Patronymic3"));
        Assert.That(managers[0].Email, Is.EqualTo(""));
        Assert.That(managers[0].Roles.Count, Is.EqualTo(0));
        Assert.That(managers[0].AccountIds.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetUsersByAccount_ReturnsForbidden_WhenManagerAccessesOtherAccount()
    {
        // Arrange
        SetCurrentUserId(2); // Manager user
        var account = new Account { Id = 102, Name = "OtherAccount" };
        _dbContext.Accounts.Add(account);
        _dbContext.Users.Add(_operatorUser);
        await _dbContext.SaveChangesAsync();

        _mockUserInformationService.Setup(x => x.CheckManager(2, 102)).ReturnsAsync(false);

        // Act
        var result = await _controller.GetUsersByAccount(102);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetUsersByAccount_ReturnsForbidden_WhenNonManagerAccesses()
    {
        // Arrange
        SetCurrentUserId(3); // Customer user (not manager)
        var account = new Account { Id = 103, Name = "TestAccount3" };
        _dbContext.Accounts.Add(account);
        _dbContext.Users.Add(_customerUser);
        await _dbContext.SaveChangesAsync();

        _mockUserInformationService.Setup(x => x.CheckManager(3, 103)).ReturnsAsync(false);

        // Act
        var result = await _controller.GetUsersByAccount(103);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetUsersByAccount_ReturnsNotFound_WhenAccountDoesNotExist()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.Add(_adminUser);
        await _dbContext.SaveChangesAsync();

        _mockUserInformationService.Setup(x => x.CheckManager(1, 999)).ReturnsAsync(true);

        // Act
        var result = await _controller.GetUsersByAccount(999);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetUsersByAccount_ReturnsEmptyList_WhenNoManagersAssigned()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        var account = new Account { Id = 104, Name = "EmptyAccount" };
        _dbContext.Accounts.Add(account);
        _dbContext.Users.Add(_adminUser);
        await _dbContext.SaveChangesAsync();

        _mockUserInformationService.Setup(x => x.CheckManager(1, 104)).ReturnsAsync(true);

        // Act
        var result = await _controller.GetUsersByAccount(104);

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        var managers = result.Value!.ToList();
        Assert.That(managers, Is.Empty);
    }

    [Test]
    public async Task GetUsersByAccount_ExcludesNonManagers_WhenUsersAssignedToAccount()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        var account = new Account { Id = 105, Name = "MixedAccount" };
        var manager = new User
        {
            Id = 7,
            FirstName = "Manager4",
            LastName = "User",
            Patronymic = "Patronymic4",
            Email = "manager4@example.com",
            Password = BCrypt.Net.BCrypt.HashPassword("password"),
            UserRoles = [new UserRole { UserId = 7, RoleId = _operatorRole.Id, Role = _operatorRole }],
            UserAccounts = [new UserAccount { UserId = 7, AccountId = 105, Account = account }]
        };
        var engineer = new User
        {
            Id = 8,
            FirstName = "Engineer",
            LastName = "User",
            Patronymic = "PatronymicEng",
            Email = "engineer@example.com",
            Password = BCrypt.Net.BCrypt.HashPassword("password"),
            UserRoles = [new UserRole { UserId = 8, RoleId = _customerRole.Id, Role = _customerRole }],
            UserAccounts = [new UserAccount { UserId = 8, AccountId = 105, Account = account }]
        };

        _dbContext.Accounts.Add(account);
        _dbContext.Users.AddRange(_adminUser, manager, engineer);
        await _dbContext.SaveChangesAsync();

        _mockUserInformationService.Setup(x => x.CheckManager(1, 105)).ReturnsAsync(true);

        // Act
        var result = await _controller.GetUsersByAccount(105);

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        var managers = result.Value!.ToList();
        Assert.That(managers, Has.Count.EqualTo(1));
        Assert.That(managers[0].FirstName, Is.EqualTo("Manager4"));
    }

    #endregion

    private void SetCurrentUserId(int userId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["UserId"] = userId;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);
        _controller = new UsersController(_mockHttpContextAccessor.Object, _dbContext, _mockUserInformationService.Object, _mockLogger.Object);
    }
}
