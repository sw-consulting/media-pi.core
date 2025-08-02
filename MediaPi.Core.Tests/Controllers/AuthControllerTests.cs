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

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

using MediaPi.Core.Authorization;
using MediaPi.Core.Controllers;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;

namespace MediaPi.Core.Tests.Controllers;

[TestFixture]
public class AuthControllerTests
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. 
    private Mock<IJwtUtils> _mockJwtUtils;
    private Mock<ILogger<AuthController>> _mockLogger;
    private AppDbContext _dbContext;
    private AuthController _controller;
    private User _testUser;
    private Role _testRole;
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

        // Setup mocks
        _mockJwtUtils = new Mock<IJwtUtils>();
        _mockLogger = new Mock<ILogger<AuthController>>();

        // Setup controller
        _controller = new AuthController(_dbContext, _mockJwtUtils.Object, _mockLogger.Object);

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
        Assert.That(userView.Roles, Contains.Item(_testRole.Name));
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
}
