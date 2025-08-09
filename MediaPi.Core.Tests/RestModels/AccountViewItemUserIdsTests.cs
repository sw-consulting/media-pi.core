// MIT License
// Copyright (c) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
//
// Unit tests for AccountViewItem.UserIds field
using NUnit.Framework;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using System.Collections.Generic;
using System.Linq;

namespace MediaPi.Core.Tests.RestModels;

[TestFixture]
public class AccountViewItemUserIdsTests
{
    private Account CreateAccount(int id, List<UserAccount>? userAccounts = null)
    {
        return new Account
        {
            Id = id,
            Name = $"Account{id}",
            UserAccounts = userAccounts ?? new List<UserAccount>()
        };
    }

    private User MakeUser(int id, List<UserRole>? roles = null)
    {
        return new User
        {
            Id = id,
            FirstName = "Test",
            LastName = "User",
            Patronymic = "",
            Email = $"user{id}@test.com",
            Password = "password",
            UserRoles = roles ?? new List<UserRole>()
        };
    }

    private UserRole MakeRole(int userId, UserRoleConstants roleConst)
    {
        return new UserRole
        {
            UserId = userId,
            RoleId = (int)roleConst,
            Role = new Role { RoleId = roleConst, Name = roleConst.ToString() }
        };
    }

    private UserAccount MakeUserAccount(int userId, int accountId, List<UserRole>? roles = null)
    {
        return new UserAccount
        {
            UserId = userId,
            AccountId = accountId,
            User = MakeUser(userId, roles),
            Account = new Account { Id = accountId, Name = $"Account{accountId}" }
        };
    }

    [Test]
    public void UserIds_Populated_ForAccountManagers()
    {
        var ua1 = MakeUserAccount(1, 10, new List<UserRole> { MakeRole(1, UserRoleConstants.AccountManager) });
        var ua2 = MakeUserAccount(2, 10, new List<UserRole> { MakeRole(2, UserRoleConstants.AccountManager) });
        var ua3 = MakeUserAccount(3, 10, new List<UserRole> { MakeRole(3, UserRoleConstants.InstallationEngineer) });
        var account = CreateAccount(10, new List<UserAccount> { ua1, ua2, ua3 });
        var viewItem = new AccountViewItem(account);
        Assert.That(viewItem.UserIds, Is.EquivalentTo(new[] { 1, 2 }));
    }

    [Test]
    public void UserIds_Empty_WhenNoAccountManagers()
    {
        var ua1 = MakeUserAccount(1, 11, new List<UserRole> { MakeRole(1, UserRoleConstants.InstallationEngineer) });
        var ua2 = MakeUserAccount(2, 11, new List<UserRole> { MakeRole(2, UserRoleConstants.SystemAdministrator) });
        var account = CreateAccount(11, new List<UserAccount> { ua1, ua2 });
        var viewItem = new AccountViewItem(account);
        Assert.That(viewItem.UserIds, Is.Empty);
    }

    [Test]
    public void UserIds_Empty_WhenNoUserAccounts()
    {
        var account = CreateAccount(12);
        var viewItem = new AccountViewItem(account);
        Assert.That(viewItem.UserIds, Is.Empty);
    }

    [Test]
    public void UserIds_Populated_OnlyForMatchingAccountId()
    {
        var ua1 = MakeUserAccount(1, 13, new List<UserRole> { MakeRole(1, UserRoleConstants.AccountManager) });
        var ua2 = MakeUserAccount(2, 99, new List<UserRole> { MakeRole(2, UserRoleConstants.AccountManager) }); // Different account
        var account = CreateAccount(13, new List<UserAccount> { ua1, ua2 });
        var viewItem = new AccountViewItem(account);
        Assert.That(viewItem.UserIds, Is.EquivalentTo(new[] { 1 }));
    }
}
