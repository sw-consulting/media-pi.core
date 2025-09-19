// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using NUnit.Framework;
using MediaPi.Core.Models;
using System.Collections.Generic;

namespace MediaPi.Core.Tests.Models;

public class UserTests
{
    [Test]
    public void HasAnyRole_ReturnsFalse_WhenNoRoles()
    {
        var user = new User { Email = "test@example.com", Password = "password123" };
        Assert.That(user.HasAnyRole(), Is.False);
    }

    [Test]
    public void HasAnyRole_ReturnsTrue_WhenRolesExist()
    {
        var role = new Role { Id = 1, RoleId = UserRoleConstants.SystemAdministrator, Name = "admin" };
        var user = new User
        {
            Email = "test@example.com",
            Password = "password123",
            UserRoles = [new() { RoleId = role.Id, Role = role }]
        };
        Assert.That(user.HasAnyRole(), Is.True);
    }

    [Test]
    public void HasRole_ReturnsTrue_WhenUserHasRole()
    {
        var role = new Role { Id = 1, RoleId = UserRoleConstants.SystemAdministrator, Name = "Admin" };
        var user = new User
        {
            Id = 1,
            Email = "test@example.com",
            Password = "password123",
            UserRoles =
            [
                new UserRole { UserId = 1, RoleId = role.Id, Role = role }
            ]
        };
        Assert.That(user.HasRole(UserRoleConstants.SystemAdministrator), Is.True);
    }

    [Test]
    public void HasRole_ReturnsFalse_WhenRoleMissing()
    {
        var user = new User { Email = "test@example.com", Password = "password123" };
        Assert.That(user.HasRole(UserRoleConstants.SystemAdministrator), Is.False);
    }

    [Test]
    public void IsAdministrator_ReturnsTrue_WhenAdminRolePresent()
    {
        var role = new Role { Id = 1, RoleId = UserRoleConstants.SystemAdministrator, Name = "administrator" };
        var user = new User
        {
            Id = 1,
            Email = "test@example.com",
            Password = "password123",
            UserRoles =
            [
                new UserRole { UserId = 1, RoleId = role.Id, Role = role }
            ]
        };
        Assert.That(user.IsAdministrator(), Is.True);
    }

    [Test]
    public void IsManager_ReturnsTrue_WhenManagerRolePresent()
    {
        var role = new Role { Id = 2, RoleId = UserRoleConstants.AccountManager, Name = "operator" };
        var user = new User
        {
            Id = 1,
            Email = "test@example.com",
            Password = "password123",
            UserRoles =
            [
                new UserRole { UserId = 1, RoleId = role.Id, Role = role }
            ]
        };
        Assert.That(user.IsManager(), Is.True);
    }

    [Test]
    public void IsManager_ReturnsFalse_WhenManagerRoleNotPresent()
    {
        var role = new Role { Id = 1, RoleId = UserRoleConstants.SystemAdministrator, Name = "admin" };
        var user = new User
        {
            Id = 1,
            Email = "test@example.com",
            Password = "password123",
            UserRoles =
            [
                new UserRole { UserId = 1, RoleId = role.Id, Role = role }
            ]
        };
        Assert.That(user.IsManager(), Is.False);
    }

    [Test]
    public void IsEngineer_ReturnsTrue_WhenEngineerRolePresent()
    {
        var role = new Role { Id = 3, RoleId = UserRoleConstants.InstallationEngineer, Name = "engineer" };
        var user = new User
        {
            Id = 1,
            Email = "test@example.com",
            Password = "password123",
            UserRoles =
            [
                new UserRole { UserId = 1, RoleId = role.Id, Role = role }
            ]
        };
        Assert.That(user.IsEngineer(), Is.True);
    }

    [Test]
    public void IsEngineer_ReturnsFalse_WhenEngineerRoleNotPresent()
    {
        var role = new Role { Id = 1, RoleId = UserRoleConstants.SystemAdministrator, Name = "admin" };
        var user = new User
        {
            Id = 1,
            Email = "test@example.com",
            Password = "password123",
            UserRoles =
            [
                new UserRole { UserId = 1, RoleId = role.Id, Role = role }
            ]
        };
        Assert.That(user.IsEngineer(), Is.False);
    }
}
