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
using Fuelflux.Core.Models;
using System.Collections.Generic;

namespace Fuelflux.Core.Tests.Models;

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
        var role = new Role { Id = 1, RoleId = UserRoleConstants.Admin, Name = "admin" };
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
        var role = new Role { Id = 1, RoleId = UserRoleConstants.Admin, Name = "Admin" };
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
        Assert.That(user.HasRole(UserRoleConstants.Admin), Is.True);
    }

    [Test]
    public void HasRole_ReturnsFalse_WhenRoleMissing()
    {
        var user = new User { Email = "test@example.com", Password = "password123" };
        Assert.That(user.HasRole(UserRoleConstants.Admin), Is.False);
    }

    [Test]
    public void IsAdministrator_ReturnsTrue_WhenAdminRolePresent()
    {
        var role = new Role { Id = 1, RoleId = UserRoleConstants.Admin, Name = "administrator" };
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
    public void IsOperator_ReturnsTrue_WhenOperatorRolePresent()
    {
        var role = new Role { Id = 2, RoleId = UserRoleConstants.Operator, Name = "operator" };
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
        Assert.That(user.IsOperator(), Is.True);
    }

    [Test]
    public void IsOperator_ReturnsFalse_WhenOperatorRoleNotPresent()
    {
        var role = new Role { Id = 1, RoleId = UserRoleConstants.Admin, Name = "admin" };
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
        Assert.That(user.IsOperator(), Is.False);
    }
}
