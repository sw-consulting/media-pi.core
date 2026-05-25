// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Controllers;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaPi.Core.Tests.Controllers;

[TestFixture]
public class CategoriesControllerTests
{
#pragma warning disable CS8618
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private Mock<ILogger<CategoriesController>> _mockLogger;
    private TestAppDbContext _dbContext;
    private CategoriesController _controller;
    private Role _adminRole;
    private Role _managerRole;
    private Role _engineerRole;
    private Category _category1;
    private Category _category2;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"categories_controller_test_db_{Guid.NewGuid()}")
            .Options;
        _dbContext = new TestAppDbContext(options);

        _adminRole = new Role { Id = 1, RoleId = UserRoleConstants.SystemAdministrator, Name = "Admin" };
        _managerRole = new Role { Id = 2, RoleId = UserRoleConstants.AccountManager, Name = "Manager" };
        _engineerRole = new Role { Id = 3, RoleId = UserRoleConstants.InstallationEngineer, Name = "Engineer" };
        _dbContext.Roles.AddRange(_adminRole, _managerRole, _engineerRole);

        _dbContext.Users.AddRange(
            CreateUser(1, "admin@example.com", _adminRole),
            CreateUser(2, "manager@example.com", _managerRole),
            CreateUser(3, "engineer@example.com", _engineerRole));

        _category1 = new Category { Id = 1, Title = "Movies", Free = true };
        _category2 = new Category { Id = 2, Title = "Sport", Free = false };
        _dbContext.Categories.AddRange(_category1, _category2);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockLogger = new Mock<ILogger<CategoriesController>>();
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private static User CreateUser(int id, string email, Role role)
    {
        return new User
        {
            Id = id,
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword("pwd"),
            UserRoles = [new UserRole { UserId = id, RoleId = role.Id, Role = role }]
        };
    }

    private void SetCurrentUser(int? id)
    {
        var context = new DefaultHttpContext();
        if (id.HasValue) context.Items["UserId"] = id.Value;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);
        _controller = new CategoriesController(
            _mockHttpContextAccessor.Object,
            _dbContext,
            _mockLogger.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    [Test]
    public async Task GetAll_Admin_ReturnsAllCategories()
    {
        SetCurrentUser(1);

        var result = await _controller.GetAll();

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Select(c => c.Id), Is.EquivalentTo(new[] { _category1.Id, _category2.Id }));
    }

    [Test]
    public async Task GetAll_Manager_ReturnsAllCategories()
    {
        SetCurrentUser(2);

        var result = await _controller.GetAll();

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task GetAll_Engineer_Returns403()
    {
        SetCurrentUser(3);

        var result = await _controller.GetAll();

        AssertObjectStatus(result.Result, StatusCodes.Status403Forbidden);
    }

    [Test]
    public async Task GetAll_NoUser_Returns403()
    {
        SetCurrentUser(null);

        var result = await _controller.GetAll();

        AssertObjectStatus(result.Result, StatusCodes.Status403Forbidden);
    }

    [Test]
    public async Task GetCategory_Manager_ReturnsCategory()
    {
        SetCurrentUser(2);

        var result = await _controller.GetCategory(_category1.Id);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Title, Is.EqualTo(_category1.Title));
        Assert.That(result.Value.Free, Is.True);
    }

    [Test]
    public async Task GetCategory_NotFound_Returns404()
    {
        SetCurrentUser(1);

        var result = await _controller.GetCategory(999);

        AssertObjectStatus(result.Result, StatusCodes.Status404NotFound);
    }

    [Test]
    public async Task CreateCategory_Admin_CreatesWithDefaultFree()
    {
        SetCurrentUser(1);
        var dto = new CategoryCreateItem { Title = "News" };

        var result = await _controller.CreateCategory(dto);

        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var created = await _dbContext.Categories.SingleAsync(c => c.Title == "News");
        Assert.That(created.Free, Is.True);
    }

    [Test]
    public async Task CreateCategory_Admin_CreatesPaidCategory()
    {
        SetCurrentUser(1);
        var dto = new CategoryCreateItem { Title = "Premium", Free = false };

        var result = await _controller.CreateCategory(dto);

        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var created = await _dbContext.Categories.SingleAsync(c => c.Title == "Premium");
        Assert.That(created.Free, Is.False);
    }

    [Test]
    public async Task CreateCategory_Manager_Returns403()
    {
        SetCurrentUser(2);

        var result = await _controller.CreateCategory(new CategoryCreateItem { Title = "Blocked" });

        AssertObjectStatus(result.Result, StatusCodes.Status403Forbidden);
        Assert.That(await _dbContext.Categories.AnyAsync(c => c.Title == "Blocked"), Is.False);
    }

    [Test]
    public async Task CreateCategory_TitleConstraint_ReturnsCustom409()
    {
        SetCurrentUser(1);
        _dbContext.SaveException = ConstraintException("IX_categories_title");

        var result = await _controller.CreateCategory(new CategoryCreateItem { Title = "Movies" });

        AssertObjectStatus(result.Result, StatusCodes.Status409Conflict);
        var error = (ErrMessage)((ObjectResult)result.Result!).Value!;
        Assert.That(error.Msg, Does.Contain("Категория"));
    }

    [Test]
    public async Task CreateCategory_TitleConstraintNameProperty_ReturnsCustom409()
    {
        SetCurrentUser(1);
        _dbContext.SaveException = ConstraintNameOnlyException("IX_categories_title");

        var result = await _controller.CreateCategory(new CategoryCreateItem { Title = "Movies" });

        AssertObjectStatus(result.Result, StatusCodes.Status409Conflict);
    }

    [Test]
    public async Task UpdateCategory_Admin_UpdatesProvidedFields()
    {
        SetCurrentUser(1);

        var result = await _controller.UpdateCategory(_category1.Id, new CategoryUpdateItem { Title = "Cinema", Free = false });

        Assert.That(result, Is.TypeOf<NoContentResult>());
        var updated = await _dbContext.Categories.FindAsync(_category1.Id);
        Assert.That(updated!.Title, Is.EqualTo("Cinema"));
        Assert.That(updated.Free, Is.False);
    }

    [Test]
    public async Task UpdateCategory_NullFields_DoNotChange()
    {
        SetCurrentUser(1);

        var result = await _controller.UpdateCategory(_category1.Id, new CategoryUpdateItem { Title = null, Free = null });

        Assert.That(result, Is.TypeOf<NoContentResult>());
        var updated = await _dbContext.Categories.FindAsync(_category1.Id);
        Assert.That(updated!.Title, Is.EqualTo("Movies"));
        Assert.That(updated.Free, Is.True);
    }

    [Test]
    public async Task UpdateCategory_Manager_Returns403()
    {
        SetCurrentUser(2);

        var result = await _controller.UpdateCategory(_category1.Id, new CategoryUpdateItem { Title = "Denied" });

        AssertObjectStatus(result, StatusCodes.Status403Forbidden);
        Assert.That((await _dbContext.Categories.FindAsync(_category1.Id))!.Title, Is.EqualTo("Movies"));
    }

    [Test]
    public async Task UpdateCategory_NotFound_Returns404()
    {
        SetCurrentUser(1);

        var result = await _controller.UpdateCategory(999, new CategoryUpdateItem { Title = "Missing" });

        AssertObjectStatus(result, StatusCodes.Status404NotFound);
    }

    [Test]
    public async Task UpdateCategory_TitleConstraint_ReturnsCustom409()
    {
        SetCurrentUser(1);
        _dbContext.SaveException = ConstraintException("IX_categories_title");

        var result = await _controller.UpdateCategory(_category1.Id, new CategoryUpdateItem { Title = "Sport" });

        AssertObjectStatus(result, StatusCodes.Status409Conflict);
        var error = (ErrMessage)((ObjectResult)result).Value!;
        Assert.That(error.Msg, Does.Contain("Sport"));
    }

    [Test]
    public async Task DeleteCategory_Admin_RemovesCategory()
    {
        SetCurrentUser(1);

        var result = await _controller.DeleteCategory(_category2.Id);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        Assert.That(await _dbContext.Categories.FindAsync(_category2.Id), Is.Null);
    }

    [Test]
    public async Task DeleteCategory_Manager_Returns403()
    {
        SetCurrentUser(2);

        var result = await _controller.DeleteCategory(_category1.Id);

        AssertObjectStatus(result, StatusCodes.Status403Forbidden);
        Assert.That(await _dbContext.Categories.FindAsync(_category1.Id), Is.Not.Null);
    }

    [Test]
    public async Task DeleteCategory_NotFound_Returns404()
    {
        SetCurrentUser(1);

        var result = await _controller.DeleteCategory(999);

        AssertObjectStatus(result, StatusCodes.Status404NotFound);
    }

    [Test]
    public async Task DeleteCategory_VideoReferenceConstraint_ReturnsCustom409()
    {
        SetCurrentUser(1);
        _dbContext.SaveException = ConstraintException("FK_videos_categories_category_id");

        var result = await _controller.DeleteCategory(_category1.Id);

        AssertObjectStatus(result, StatusCodes.Status409Conflict);
        var error = (ErrMessage)((ObjectResult)result).Value!;
        Assert.That(error.Msg, Does.Contain("используется"));
    }

    [Test]
    public async Task DeleteCategory_SubscriptionReferenceConstraint_ReturnsCustom409()
    {
        SetCurrentUser(1);
        _dbContext.SaveException = ConstraintException("FK_subscriptions_categories_category_id");

        var result = await _controller.DeleteCategory(_category1.Id);

        AssertObjectStatus(result, StatusCodes.Status409Conflict);
    }

    private static DbUpdateException ConstraintException(string constraintName)
    {
        return new DbUpdateException(
            $"Simulated database constraint violation: {constraintName}",
            new InvalidOperationException($"violates constraint \"{constraintName}\""));
    }

    private static DbUpdateException ConstraintNameOnlyException(string constraintName)
    {
        return new DbUpdateException(
            "Simulated database constraint violation",
            new ProviderConstraintException(constraintName));
    }

    private static void AssertObjectStatus(IActionResult? result, int statusCode)
    {
        Assert.That(result, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)result!).StatusCode, Is.EqualTo(statusCode));
    }

    private sealed class TestAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        public DbUpdateException? SaveException { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (SaveException != null)
            {
                throw SaveException;
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class ProviderConstraintException(string constraintName) : Exception("provider constraint violation")
    {
        public string ConstraintName { get; } = constraintName;
    }
}
