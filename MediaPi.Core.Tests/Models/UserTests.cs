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
}
