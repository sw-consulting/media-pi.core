// Developed by Maxim [maxirmx] Samsonov (www.sw.consulting)
// This file is a part of Media Pi backend application

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Threading.Tasks;
using System.Threading;

using Moq;
using NUnit.Framework;

using MediaPi.Core.Authorization;
using MediaPi.Core.Controllers;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Settings;

namespace MediaPi.Core.Tests.Controllers;

[TestFixture]
public class AuthControllerTests
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. 
    private Mock<IJwtUtils> _mockJwtUtils;
    private Mock<ILogger<AuthController>> _mockLogger;
    private Mock<IOptions<AppSettings>> _mockAppSettings;
    private AppDbContext _dbContext;
    private AuthController _controller;
    private User _testUser;
    private Role _testRole;
    private AppSettings _appSettings;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor.

    [SetUp]
    public void Setup()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"auth_controller_test_db_{System.Guid.NewGuid()}")
            .Options;

        _dbContext = new AppDbContext(options);

        // Add a role
        _testRole = new Role { Id = (int)UserRoleConstants.AccountManager, Name = "Менеджер лицевого счёта" };
        _dbContext.Roles.Add(_testRole);

        // Setup app settings
        _appSettings = new AppSettings
        {
            Secret = "test-secret-key",
            Token = "test-gateway-token",
            JwtTokenExpirationDays = 7
        };

        // Setup mocks
        _mockJwtUtils = new Mock<IJwtUtils>();
        _mockLogger = new Mock<ILogger<AuthController>>();
        _mockAppSettings = new Mock<IOptions<AppSettings>>();
        _mockAppSettings.Setup(x => x.Value).Returns(_appSettings);

        // Setup controller
        _controller = new AuthController(_dbContext, _mockJwtUtils.Object, _mockAppSettings.Object, _mockLogger.Object);

        // Create test user with hashed password
        string hashedPassword = BCrypt.Net.BCrypt.HashPassword("password123");
        _testUser = new User
        {
            Id = 1,
            Email = "test@example.com",
            Password = hashedPassword,
            FirstName = "Test",
            LastName = "User",
            Patronymic = "",
            UserRoles =
            [
                new UserRole { UserId = 1, RoleId = 1, Role = _testRole }
            ]
        };
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Test]
    public async Task Login_ReturnsToken_WhenCredentialsAreValid()
    {
        // Arrange
        _dbContext.Users.Add(_testUser);
        await _dbContext.SaveChangesAsync();

        var credentials = new Credentials
        {
            Email = "test@example.com",
            Password = "password123"
        };

        _mockJwtUtils.Setup(x => x.GenerateJwtToken(It.IsAny<User>()))
            .Returns("test-jwt-token");

        // Act
        var result = await _controller.Login(credentials);

        // Assert
        Assert.That(result.Result, Is.Null);
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value, Is.TypeOf<UserViewItemWithJWT>());

        var userView = result.Value as UserViewItemWithJWT;
        Assert.That(userView!.Token, Is.EqualTo("test-jwt-token"));
        Assert.That(userView.Email, Is.EqualTo("test@example.com"));
        Assert.That(userView.Roles, Contains.Item(_testRole.RoleId));
    }

    [Test]
    public async Task Login_ReturnsUnauthorized_WhenUserDoesNotExist()
    {
        // Arrange
        var credentials = new Credentials
        {
            Email = "nonexistent@example.com",
            Password = "password123"
        };

        // Act
        var result = await _controller.Login(credentials);

        // Assert
        Assert.That(result.Result, Is.Not.Null);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
        Assert.That(objectResult.Value, Is.TypeOf<ErrMessage>());
    }

    [Test]
    public async Task Login_ReturnsUnauthorized_WhenPasswordIsInvalid()
    {
        // Arrange
        _dbContext.Users.Add(_testUser);
        await _dbContext.SaveChangesAsync();

        var credentials = new Credentials
        {
            Email = "test@example.com",
            Password = "wrong-password"
        };

        // Act
        var result = await _controller.Login(credentials);

        // Assert
        Assert.That(result.Result, Is.Not.Null);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
        Assert.That(objectResult.Value, Is.TypeOf<ErrMessage>());
    }

    [Test]
    public async Task Login_ReturnsForbidden_WhenUserHasNoRoles()
    {
        // Arrange
        // Create user without roles
        var userWithoutRoles = new User
        {
            Id = 2,
            Email = "noroles@example.com",
            Password = BCrypt.Net.BCrypt.HashPassword("password123"),
            FirstName = "No",
            LastName = "Roles",
            Patronymic = "",
            UserRoles = []
        };

        _dbContext.Users.Add(userWithoutRoles);
        await _dbContext.SaveChangesAsync();

        var credentials = new Credentials
        {
            Email = "noroles@example.com",
            Password = "password123"
        };

        // Act
        var result = await _controller.Login(credentials);

        // Assert
        Assert.That(result.Result, Is.Not.Null);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public void Check_ReturnsNoContent_WhenAuthenticated()
    {
        // Act
        var result = _controller.Check();

        // Assert
        Assert.That(result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task Login_IncludesAccountIds_WhenUserIsManager()
    {
        // Arrange
        var account1 = new Account { Id = 1, Name = "Test Account 1" };
        var account2 = new Account { Id = 2, Name = "Test Account 2" };
        _dbContext.Accounts.AddRange(account1, account2);

        var managerRole = new Role { Id = 100, RoleId = UserRoleConstants.AccountManager, Name = "Manager" };
        _dbContext.Roles.Add(managerRole);

        string hashedPassword = BCrypt.Net.BCrypt.HashPassword("password123");
        var managerUser = new User
        {
            Id = 2,
            Email = "manager@example.com",
            Password = hashedPassword,
            FirstName = "Manager",
            LastName = "User",
            Patronymic = "",
            UserRoles =
            [
                new UserRole { UserId = 2, RoleId = managerRole.Id, Role = managerRole }
            ],
            UserAccounts =
            [
                new UserAccount { UserId = 2, AccountId = account1.Id, Account = account1 },
                new UserAccount { UserId = 2, AccountId = account2.Id, Account = account2 }
            ]
        };

        _dbContext.Users.Add(managerUser);
        await _dbContext.SaveChangesAsync();

        var credentials = new Credentials
        {
            Email = "manager@example.com",
            Password = "password123"
        };

        _mockJwtUtils.Setup(x => x.GenerateJwtToken(It.IsAny<User>()))
            .Returns("manager-jwt-token");

        // Act
        var result = await _controller.Login(credentials);

        // Assert
        Assert.That(result.Result, Is.Null);
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value, Is.TypeOf<UserViewItemWithJWT>());

        var userView = result.Value as UserViewItemWithJWT;
        Assert.That(userView!.Token, Is.EqualTo("manager-jwt-token"));
        Assert.That(userView.Email, Is.EqualTo("manager@example.com"));
        Assert.That(userView.Roles, Contains.Item(UserRoleConstants.AccountManager));
        Assert.That(userView.AccountIds, Is.EquivalentTo(new[] { 1, 2 }));
    }

    [Test]
    public async Task Login_EmptyAccountIds_WhenUserIsNotManager()
    {
        // Arrange
        var engineerRole = new Role { Id = 101, RoleId = UserRoleConstants.InstallationEngineer, Name = "Engineer" };
        _dbContext.Roles.Add(engineerRole);

        string hashedPassword = BCrypt.Net.BCrypt.HashPassword("password123");
        var engineerUser = new User
        {
            Id = 3,
            Email = "engineer@example.com",
            Password = hashedPassword,
            FirstName = "Engineer",
            LastName = "User",
            Patronymic = "",
            UserRoles =
            [
                new UserRole { UserId = 3, RoleId = engineerRole.Id, Role = engineerRole }
            ]
        };

        _dbContext.Users.Add(engineerUser);
        await _dbContext.SaveChangesAsync();

        var credentials = new Credentials
        {
            Email = "engineer@example.com",
            Password = "password123"
        };

        _mockJwtUtils.Setup(x => x.GenerateJwtToken(It.IsAny<User>()))
            .Returns("engineer-jwt-token");

        // Act
        var result = await _controller.Login(credentials);

        // Assert
        Assert.That(result.Result, Is.Null);
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value, Is.TypeOf<UserViewItemWithJWT>());

        var userView = result.Value as UserViewItemWithJWT;
        Assert.That(userView!.Token, Is.EqualTo("engineer-jwt-token"));
        Assert.That(userView.Email, Is.EqualTo("engineer@example.com"));
        Assert.That(userView.Roles, Contains.Item(UserRoleConstants.InstallationEngineer));
        Assert.That(userView.AccountIds, Is.Empty); // Non-managers should have empty AccountIds
    }

}
