// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Fuelflux core application
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
// 1. Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

using Fuelflux.Core.Controllers;
using Fuelflux.Core.Data;
using Fuelflux.Core.Models;
using Fuelflux.Core.RestModels;
using Fuelflux.Core.Services;

namespace Fuelflux.Core.Tests.Controllers;

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
        _adminRole = new Role { RoleId = UserRoleConstants.Admin, Name = "Администратор" };
        _operatorRole = new Role { RoleId = UserRoleConstants.Operator, Name = "Оператор" };
        _customerRole = new Role { RoleId = UserRoleConstants.Customer, Name = "Клиент" };
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

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(3, 1)).ReturnsAsync(new ActionResult<bool>(true));
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

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(2, 2)).ReturnsAsync(new ActionResult<bool>(true));
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

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(1, 2)).ReturnsAsync(new ActionResult<bool>(false));

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

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(999, 1)).ReturnsAsync(new ActionResult<bool>(true));
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

    #region PostUser Tests

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
            Roles = [ UserRoleConstants.Customer ] // Assigning role by enum value
        };

        _mockUserInformationService.Setup(x => x.CheckAdmin(1)).ReturnsAsync(true);
        _mockUserInformationService.Setup(x => x.Exists("new@example.com")).Returns(false);

        // Act
        var result = await _controller.PostUser(newUser);

        // Assert
        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var createdAtActionResult = result.Result as CreatedAtActionResult;
        Assert.That(createdAtActionResult!.ActionName, Is.EqualTo(nameof(UsersController.PostUser)));
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
        var result = await _controller.PostUser(newUser);

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
        var result = await _controller.PostUser(newUser);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    #endregion

    #region PutUser Tests

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
            Roles = [UserRoleConstants.Customer]
        };

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(2, 1)).ReturnsAsync(new ActionResult<bool>(true));
        _mockUserInformationService.Setup(x => x.Exists("updated@example.com")).Returns(false);

        // Act
        var result = await _controller.PutUser(2, updateItem);

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
        Assert.That(updatedUser.UserRoles.First().Role!.RoleId, Is.EqualTo(UserRoleConstants.Customer));
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

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(2, 2)).ReturnsAsync(new ActionResult<bool>(true));

        // Act
        var result = await _controller.PutUser(2, updateItem);

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

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(1, 2)).ReturnsAsync(new ActionResult<bool>(false));

        // Act
        var result = await _controller.PutUser(1, updateItem);

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
        var result = await _controller.PutUser(999, updateItem);

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

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(2, 1)).ReturnsAsync(new ActionResult<bool>(true));
        _mockUserInformationService.Setup(x => x.Exists("admin@example.com")).Returns(true);

        // Act
        var result = await _controller.PutUser(2, updateItem);

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

        _mockUserInformationService.Setup(x => x.CheckAdminOrSameUser(2, 1)).ReturnsAsync(new ActionResult<bool>(true));

        // Act
        var result = await _controller.PutUser(2, updateItem);

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
            Roles = [UserRoleConstants.Admin] // Trying to become admin
        };

        _mockUserInformationService.Setup(x => x.CheckAdmin(2)).ReturnsAsync(false);

        // Act
        var result = await _controller.PutUser(2, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objectResult = result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
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

    private void SetCurrentUserId(int userId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["UserId"] = userId;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);
        _controller = new UsersController(_mockHttpContextAccessor.Object, _dbContext, _mockUserInformationService.Object, _mockLogger.Object);
    }
}
