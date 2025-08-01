// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Fuelflux Core application
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

using NUnit.Framework;
using Microsoft.EntityFrameworkCore;
using Fuelflux.Core.Data;
using Fuelflux.Core.Models;
using Fuelflux.Core.Services;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace Fuelflux.Core.Tests.Data;

public class UserInformationServiceTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"test_db_{System.Guid.NewGuid()}")
            .Options;

        var context = new AppDbContext(options);

        // Pre-seed the roles that are needed for tests
        context.Roles.AddRange(
            new Role { RoleId = UserRoleConstants.Customer, Name = "Клиент" },
            new Role { RoleId = UserRoleConstants.Operator, Name = "Оператор АЗС" },
            new Role { RoleId = UserRoleConstants.Admin, Name = "Администратор системы" }
        );

        context.SaveChanges();

        return context;
    }

    private static Role GetAdminRole(AppDbContext ctx)
    {
        return ctx.Roles.Single(r => r.RoleId == UserRoleConstants.Admin);
    }

    private static Role GetOperatorRole(AppDbContext ctx)
    {
        return ctx.Roles.Single(r => r.RoleId == UserRoleConstants.Operator);
    }

    private static User CreateUser(int id, string email, string password, string firstName, string lastName, string? patronymic, IEnumerable<Role> roles)
    {
        return new User
        {
            Id = id,
            Email = email,
            Password = password,
            FirstName = firstName,
            LastName = lastName,
            Patronymic = patronymic ?? "",
            UserRoles = [.. roles.Select(r => new UserRole
            {
                UserId = id,
                RoleId = r.Id,
                Role = r
            })]
        };
    }

    #region CheckSameUser Tests

    [Test]
    public void CheckSameUser_ReturnsTrue_WhenIdsMatch()
    {
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        Assert.That(service.CheckSameUser(1, 1), Is.True);
    }

    [Test]
    public void CheckSameUser_ReturnsFalse_WhenIdsDiffer()
    {
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        Assert.That(service.CheckSameUser(1, 2), Is.False);
    }

    [Test]
    public void CheckSameUser_ReturnsFalse_WhenCuidZero()
    {
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        Assert.That(service.CheckSameUser(1, 0), Is.False);
    }

    #endregion

    #region CheckAdmin Tests

    [Test]
    public async Task CheckAdmin_ReturnsTrue_WhenUserIsAdmin()
    {
        // Arrange
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        var user = CreateUser(10, "admin@test.com", "password", "Admin", "User", null, [GetAdminRole(ctx)]);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        // Act
        var result = await service.CheckAdmin(10);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task CheckAdmin_ReturnsFalse_WhenUserIsNotAdmin()
    {
        // Arrange
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);

        var user = CreateUser(11, "operator@test.com", "password", "Operator", "User", null, [GetOperatorRole(ctx)]);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        // Act
        var result = await service.CheckAdmin(11);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task CheckAdmin_ReturnsFalse_WhenUserDoesNotExist()
    {
        // Arrange
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);

        // Act
        var result = await service.CheckAdmin(999);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task CheckAdmin_ReturnsFalse_WhenUserHasNoRoles()
    {
        // Arrange
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        var user = CreateUser(12, "norole@test.com", "password", "No", "Role", null, []);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        // Act
        var result = await service.CheckAdmin(12);

        // Assert
        Assert.That(result, Is.False);
    }

    #endregion

    #region CheckOperator Tests

    [Test]
    public async Task CheckOperator_ReturnsTrue_WhenUserIsOperator()
    {
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        var user = CreateUser(30, "operator@test.com", "password", "Operator", "User", null, [GetOperatorRole(ctx)]);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var result = await service.CheckOperator(30);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task CheckOperator_ReturnsFalse_WhenUserIsNotOperator()
    {
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        var user = CreateUser(31, "adminonly@test.com", "password", "Admin", "User", null, [GetAdminRole(ctx)]);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var result = await service.CheckOperator(31);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task CheckOperator_ReturnsFalse_WhenUserDoesNotExist()
    {
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);

        var result = await service.CheckOperator(999);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task CheckOperator_ReturnsFalse_WhenUserHasNoRoles()
    {
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        var user = CreateUser(32, "noroleoperator@test.com", "password", "No", "Role", null, []);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var result = await service.CheckOperator(32);

        Assert.That(result, Is.False);
    }

    #endregion

    #region CheckAdminOrSameUser Tests

    [Test]
    public async Task CheckAdminOrSameUser_ReturnsTrue_WhenSameUser()
    {
        // Arrange
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);

        // Act
        var result = await service.CheckAdminOrSameUser(5, 5);

        // Assert
        Assert.That(result.Value, Is.True);
    }

    [Test]
    public async Task CheckAdminOrSameUser_ReturnsFalse_WhenZeroCuid()
    {
        // Arrange
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);

        // Act
        var result = await service.CheckAdminOrSameUser(5, 0);

        // Assert
        Assert.That(result.Value, Is.False);
    }

    [Test]
    public async Task CheckAdminOrSameUser_ReturnsTrue_WhenAdmin()
    {
        // Arrange
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        var user = CreateUser(20, "admin2@test.com", "password", "Admin", "Two", null, [GetAdminRole(ctx)]);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        // Act
        var result = await service.CheckAdminOrSameUser(5, 20);

        // Assert
        Assert.That(result.Value, Is.True);
    }

    [Test]
    public async Task CheckAdminOrSameUser_ReturnsFalse_WhenNotAdminAndNotSameUser()
    {
        // Arrange
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        var user = CreateUser(21, "operator2@test.com", "password", "Operator", "Two", null, [GetOperatorRole(ctx)]);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        // Act
        var result = await service.CheckAdminOrSameUser(5, 21);

        // Assert
        Assert.That(result.Value, Is.False);
    }

    #endregion

    #region Exists Tests

    [Test]
    public void Exists_ReturnsTrue_WhenUserIdExists()
    {
        // Arrange
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        ctx.Users.Add(CreateUser(30, "exists@test.com", "password", "Exists", "User", null, []));
        ctx.SaveChanges();
        // Act
        var result = service.Exists(30);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void Exists_ReturnsFalse_WhenUserIdDoesNotExist()
    {
        // Arrange
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);

        // Act
        var result = service.Exists(999);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void Exists_ReturnsTrue_WhenEmailExists()
    {
        // Arrange
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        ctx.Users.Add(CreateUser(31, "email_exists@test.com", "password", "Email", "Exists", null, []));
        ctx.SaveChanges();
        // Act
        var result = service.Exists("email_exists@test.com");

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void Exists_ReturnsFalse_WhenEmailDoesNotExist()
    {
        // Arrange
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);

        // Act
        var result = service.Exists("nonexistent@test.com");

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void Exists_ReturnsTrue_WhenEmailExistsCaseInsensitive()
    {
        // Arrange
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        ctx.Users.Add(CreateUser(32, "case_test@test.com", "password", "Case", "Test", null, []));
        ctx.SaveChanges();
        // Act
        var result = service.Exists("CASE_TEST@test.com");

        // Assert
        Assert.That(result, Is.True);
    }

    #endregion

    #region UserViewItem Tests

    [Test]
    public async Task UserViewItem_ReturnsUserViewItem_WhenUserExists()
    {
        // Arrange
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        var user = CreateUser(40, "viewitem@test.com", "password", "View", "Item", "Test", [GetOperatorRole(ctx)]);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        // Act
        var result = await service.UserViewItem(40);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(40));
        Assert.That(result.Email, Is.EqualTo("viewitem@test.com"));
        Assert.That(result.FirstName, Is.EqualTo("View"));
        Assert.That(result.LastName, Is.EqualTo("Item"));
        Assert.That(result.Patronymic, Is.EqualTo("Test"));
        Assert.That(result.Roles, Has.Count.EqualTo(1));
        Assert.That(result.Roles.First(), Is.EqualTo(GetOperatorRole(ctx).Name));
    }

    [Test]
    public async Task UserViewItem_ReturnsNull_WhenUserDoesNotExist()
    {
        // Arrange
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);

        // Act
        var result = await service.UserViewItem(999);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task UserViewItem_ReturnsCorrectRoles_WhenUserHasMultipleRoles()
    {
        // Arrange
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        var user = CreateUser(41, "multirole@test.com", "password", "Multi", "Role", "", [GetOperatorRole(ctx), GetAdminRole(ctx)]);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        // Act
        var result = await service.UserViewItem(41);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Roles, Has.Count.EqualTo(2));
        Assert.That(result.Roles, Contains.Item(GetOperatorRole(ctx).Name));
        Assert.That(result.Roles, Contains.Item(GetAdminRole(ctx).Name));
    }

    #endregion

    #region UserViewItems Tests

    [Test]
    public async Task UserViewItems_ReturnsAllUsers()
    {
        // Arrange
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        ctx.Users.Add(CreateUser(50, "user1@test.com", "password", "User", "One", null, [GetOperatorRole(ctx)]));
        ctx.Users.Add(CreateUser(51, "user2@test.com", "password", "User", "Two", null, [GetAdminRole(ctx)]));

        await ctx.SaveChangesAsync();

        // Act
        var results = await service.UserViewItems();

        // Assert
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(results.Any(u => u.Id == 50), Is.True);
        Assert.That(results.Any(u => u.Id == 51), Is.True);

        var user1 = results.FirstOrDefault(u => u.Id == 50);
        var user2 = results.FirstOrDefault(u => u.Id == 51);

        Assert.That(user1?.Roles.Contains(GetOperatorRole(ctx).Name), Is.True);
        Assert.That(user2?.Roles.Contains(GetAdminRole(ctx).Name), Is.True);
    }

    [Test]
    public async Task UserViewItems_ReturnsEmptyList_WhenNoUsers()
    {
        // Arrange
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        // Clear users table - only do this in a test-specific database
        foreach (var user in ctx.Users.ToList())
        {
            ctx.Users.Remove(user);
        }
        await ctx.SaveChangesAsync();

        // Act
        var results = await service.UserViewItems();

        // Assert
        Assert.That(results, Is.Empty);
    }

    #endregion

}
