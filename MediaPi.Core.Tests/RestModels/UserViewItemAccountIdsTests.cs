// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using NUnit.Framework;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using System.Collections.Generic;

namespace MediaPi.Core.Tests.RestModels;

[TestFixture]
public class UserViewItemAccountIdsTests
{
    private User CreateUser(int id, List<UserRole> roles, List<UserAccount>? accounts = null)
    {
        return new User
        {
            Id = id,
            FirstName = "Test",
            LastName = "User",
            Patronymic = "",
            Email = $"user{id}@test.com",
            Password = "password",
            UserRoles = roles,
            UserAccounts = accounts ?? new List<UserAccount>()
        };
    }

    private UserRole MakeRole(int userId, UserRoleConstants roleConst, Role? role = null)
    {
        return new UserRole
        {
            UserId = userId,
            RoleId = (int)roleConst,
            Role = role ?? new Role { RoleId = roleConst, Name = roleConst.ToString() }
        };
    }

    private UserAccount MakeAccount(int userId, int accountId)
    {
        return new UserAccount
        {
            UserId = userId,
            AccountId = accountId,
            Account = new Account { Id = accountId, Name = $"Account{accountId}" }
        };
    }

    [Test]
    public void AccountIds_Populated_ForAccountManagerWithAccounts()
    {
        var user = CreateUser(
            1,
            new List<UserRole> { MakeRole(1, UserRoleConstants.AccountManager) },
            new List<UserAccount> { MakeAccount(1, 100), MakeAccount(1, 101) }
        );
        var viewItem = new UserViewItem(user);
        Assert.That(viewItem.AccountIds, Is.EquivalentTo(new[] { 100, 101 }));
    }

    [Test]
    public void AccountIds_Empty_ForAccountManagerWithoutAccounts()
    {
        var user = CreateUser(
            2,
            new List<UserRole> { MakeRole(2, UserRoleConstants.AccountManager) }
        );
        var viewItem = new UserViewItem(user);
        Assert.That(viewItem.AccountIds, Is.Empty);
    }

    [Test]
    public void AccountIds_Empty_ForNonAccountManagerWithAccounts()
    {
        var user = CreateUser(
            3,
            new List<UserRole> { MakeRole(3, UserRoleConstants.InstallationEngineer) },
            new List<UserAccount> { MakeAccount(3, 102) }
        );
        var viewItem = new UserViewItem(user);
        Assert.That(viewItem.AccountIds, Is.Empty);
    }

    [Test]
    public void AccountIds_Empty_ForUserWithNoRolesOrAccounts()
    {
        var user = CreateUser(4, new List<UserRole>());
        var viewItem = new UserViewItem(user);
        Assert.That(viewItem.AccountIds, Is.Empty);
    }
}
