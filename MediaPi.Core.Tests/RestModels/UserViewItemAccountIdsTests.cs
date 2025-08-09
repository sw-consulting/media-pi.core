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
