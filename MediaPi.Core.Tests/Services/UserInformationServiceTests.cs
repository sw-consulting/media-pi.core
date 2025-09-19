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
using Microsoft.EntityFrameworkCore;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.Services;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace MediaPi.Core.Tests.Services;

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
            new Role { RoleId = UserRoleConstants.InstallationEngineer, Name = "Инженер-установщик" },
            new Role { RoleId = UserRoleConstants.AccountManager, Name = "Менеджер лицевого счёта" },
            new Role { RoleId = UserRoleConstants.SystemAdministrator, Name = "Администратор системы" }
        );

        context.SaveChanges();

        return context;
    }

    private static Role GetAdminRole(AppDbContext ctx)
    {
        return ctx.Roles.Single(r => r.RoleId == UserRoleConstants.SystemAdministrator);
    }

    private static Role GetOperatorRole(AppDbContext ctx)
    {
        return ctx.Roles.Single(r => r.RoleId == UserRoleConstants.AccountManager);
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

    #region IsManager Tests

    [Test]
    public async Task IsManager_ReturnsTrue_ForAdmin()
    {
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        var account = new Account { Id = 1, Name = "acc" };
        ctx.Accounts.Add(account);
        var user = CreateUser(30, "admin@test.com", "password", "Admin", "User", null, [GetAdminRole(ctx)]);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var result = await service.CheckManager(30, 1);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task IsManager_ReturnsTrue_WhenManagerLinkedToAccount()
    {
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        var account = new Account { Id = 2, Name = "acc" };
        ctx.Accounts.Add(account);
        var user = CreateUser(31, "manager@test.com", "password", "Manager", "User", null, [GetOperatorRole(ctx)]);
        user.UserAccounts = [new UserAccount { UserId = 31, AccountId = 2, Account = account }];
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var result = await service.CheckManager(31, 2);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task IsManager_ReturnsFalse_WhenManagerNotLinkedToAccount()
    {
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        var account = new Account { Id = 3, Name = "acc" };
        ctx.Accounts.Add(account);
        var user = CreateUser(32, "manager2@test.com", "password", "Manager", "User", null, [GetOperatorRole(ctx)]);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var result = await service.CheckManager(32, 3);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task IsManager_ReturnsFalse_WhenUserHasNoRoles()
    {
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        var account = new Account { Id = 4, Name = "acc" };
        ctx.Accounts.Add(account);
        var user = CreateUser(33, "noroleoperator@test.com", "password", "No", "Role", null, []);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var result = await service.CheckManager(33, 4);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task IsManager_ReturnsFalse_WhenUserDoesNotExist()
    {
        using var ctx = CreateContext();
        var service = new UserInformationService(ctx);
        var account = new Account { Id = 5, Name = "acc" };
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var result = await service.CheckManager(999, 5);

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
        Assert.That(result, Is.True);
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
        Assert.That(result, Is.False);
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
        Assert.That(result, Is.True);
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
        Assert.That(result, Is.False);
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
        Assert.That(result.Roles.First(), Is.EqualTo(GetOperatorRole(ctx).RoleId));
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
        Assert.That(result.Roles, Contains.Item(GetOperatorRole(ctx).RoleId));
        Assert.That(result.Roles, Contains.Item(GetAdminRole(ctx).RoleId));
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

        Assert.That(user1?.Roles.Contains(GetOperatorRole(ctx).RoleId), Is.True);
        Assert.That(user2?.Roles.Contains(GetAdminRole(ctx).RoleId), Is.True);
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

    #region Permission helpers

    [Test]
    public void UserCanViewDevice_AdminSeesAll()
    {
        using var ctx = CreateContext();
        var svc = new UserInformationService(ctx);

        var acc1 = new Account { Id = 101, Name = "A101" };
        var acc2 = new Account { Id = 102, Name = "A102" };
        ctx.Accounts.AddRange(acc1, acc2);

        var d1 = new Device { Id = 201, Name = "D201", IpAddress = "10.0.0.1", AccountId = acc1.Id };
        var d2 = new Device { Id = 202, Name = "D202", IpAddress = "10.0.0.2", AccountId = acc2.Id };
        var d3 = new Device { Id = 203, Name = "D203", IpAddress = "10.0.0.3" };
        ctx.Devices.AddRange(d1, d2, d3);

        var admin = CreateUser(900, "admin@x", "pwd", "A", "B", null, [GetAdminRole(ctx)]);
        ctx.Users.Add(admin);
        ctx.SaveChanges();

        var trackedAdmin = ctx.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).Single(u => u.Id == 900);

        Assert.That(svc.UserCanViewDevice(trackedAdmin, d1));
        Assert.That(svc.UserCanViewDevice(trackedAdmin, d2));
        Assert.That(svc.UserCanViewDevice(trackedAdmin, d3));
    }

    [Test]
    public void ManagerSeesOwnedOnly()
    {
        using var ctx = CreateContext();
        var svc = new UserInformationService(ctx);

        var acc1 = new Account { Id = 111, Name = "A111" };
        var acc2 = new Account { Id = 112, Name = "A112" };
        ctx.Accounts.AddRange(acc1, acc2);

        var d1 = new Device { Id = 301, Name = "D301", IpAddress = "10.1.0.1", AccountId = acc1.Id };
        var d2 = new Device { Id = 302, Name = "D302", IpAddress = "10.1.0.2", AccountId = acc2.Id };
        var d3 = new Device { Id = 303, Name = "D303", IpAddress = "10.1.0.3" };
        ctx.Devices.AddRange(d1, d2, d3);

        var mgr = CreateUser(901, "mgr@x", "pwd", "M", "G", null, [GetOperatorRole(ctx)]);
        mgr.UserAccounts = [new UserAccount { UserId = 901, AccountId = acc1.Id, Account = acc1 }];
        ctx.Users.Add(mgr);
        ctx.SaveChanges();

        var trackedMgr = ctx.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).Include(u => u.UserAccounts).Single(u => u.Id == 901);

        Assert.That(svc.UserCanViewDevice(trackedMgr, d1));
        Assert.That(svc.UserCanViewDevice(trackedMgr, d2), Is.False);
        Assert.That(svc.UserCanViewDevice(trackedMgr, d3), Is.False);
    }

    [Test]
    public void EngineerSeesUnassignedOnly()
    {
        using var ctx = CreateContext();
        var svc = new UserInformationService(ctx);

        var acc1 = new Account { Id = 121, Name = "A121" };
        ctx.Accounts.Add(acc1);

        var d1 = new Device { Id = 401, Name = "D401", IpAddress = "10.2.0.1", AccountId = acc1.Id };
        var d3 = new Device { Id = 403, Name = "D403", IpAddress = "10.2.0.3" };
        ctx.Devices.AddRange(d1, d3);

        var eng = CreateUser(902, "eng@x", "pwd", "E", "N", null, [ctx.Roles.Single(r => r.RoleId == UserRoleConstants.InstallationEngineer)]);
        ctx.Users.Add(eng);
        ctx.SaveChanges();

        var trackedEng = ctx.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).Single(u => u.Id == 902);

        Assert.That(svc.UserCanViewDevice(trackedEng, d1), Is.False);
        Assert.That(svc.UserCanViewDevice(trackedEng, d3));
    }

    [Test]
    public void UserCanAssignGroup_AdminAndManager()
    {
        using var ctx = CreateContext();
        var svc = new UserInformationService(ctx);

        var acc1 = new Account { Id = 131, Name = "A131" };
        var acc2 = new Account { Id = 132, Name = "A132" };
        ctx.Accounts.AddRange(acc1, acc2);

        var d1 = new Device { Id = 501, Name = "D501", IpAddress = "10.3.0.1", AccountId = acc1.Id };
        var d2 = new Device { Id = 502, Name = "D502", IpAddress = "10.3.0.2", AccountId = acc2.Id };
        ctx.Devices.AddRange(d1, d2);

        var admin = CreateUser(910, "admin2@x", "pwd", "A2", "B2", null, [GetAdminRole(ctx)]);
        ctx.Users.Add(admin);

        var mgr = CreateUser(911, "mgr2@x", "pwd", "M2", "G2", null, [GetOperatorRole(ctx)]);
        mgr.UserAccounts = [new UserAccount { UserId = 911, AccountId = acc1.Id, Account = acc1 }];
        ctx.Users.Add(mgr);

        var eng = CreateUser(912, "eng2@x", "pwd", "E2", "N2", null, [ctx.Roles.Single(r => r.RoleId == UserRoleConstants.InstallationEngineer)]);
        ctx.Users.Add(eng);

        ctx.SaveChanges();

        var trackedAdmin = ctx.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).Single(u => u.Id == 910);
        var trackedMgr = ctx.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).Include(u => u.UserAccounts).Single(u => u.Id == 911);
        var trackedEng = ctx.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).Single(u => u.Id == 912);

        Assert.That(svc.UserCanAssignGroup(trackedAdmin, d1));
        Assert.That(svc.UserCanAssignGroup(trackedMgr, d1));
        Assert.That(svc.UserCanAssignGroup(trackedMgr, d2), Is.False);
        Assert.That(svc.UserCanAssignGroup(trackedEng, d1), Is.False);
    }

    [Test]
    public void UserCanManageDeviceServices_ExpectedBehavior()
    {
        using var ctx = CreateContext();
        var svc = new UserInformationService(ctx);

        var acc1 = new Account { Id = 141, Name = "A141" };
        ctx.Accounts.Add(acc1);

        var dAssigned = new Device { Id = 601, Name = "D601", IpAddress = "10.4.0.1", AccountId = acc1.Id };
        var dUnassigned = new Device { Id = 602, Name = "D602", IpAddress = "10.4.0.2" };
        ctx.Devices.AddRange(dAssigned, dUnassigned);

        var admin = CreateUser(920, "admin3@x", "pwd", "A3", "B3", null, [GetAdminRole(ctx)]);
        ctx.Users.Add(admin);

        var mgr = CreateUser(921, "mgr3@x", "pwd", "M3", "G3", null, [GetOperatorRole(ctx)]);
        mgr.UserAccounts = [new UserAccount { UserId = 921, AccountId = acc1.Id, Account = acc1 }];
        ctx.Users.Add(mgr);

        var eng = CreateUser(922, "eng3@x", "pwd", "E3", "N3", null, [ctx.Roles.Single(r => r.RoleId == UserRoleConstants.InstallationEngineer)]);
        ctx.Users.Add(eng);

        ctx.SaveChanges();

        var tAdmin = ctx.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).Single(u => u.Id == 920);
        var tMgr = ctx.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).Include(u => u.UserAccounts).Single(u => u.Id == 921);
        var tEng = ctx.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).Single(u => u.Id == 922);

        Assert.That(svc.UserCanManageDeviceServices(tAdmin, dAssigned));
        Assert.That(svc.UserCanManageDeviceServices(tMgr, dAssigned));
        Assert.That(svc.UserCanManageDeviceServices(tMgr, dUnassigned), Is.False);
        Assert.That(svc.UserCanManageDeviceServices(tEng, dUnassigned));
        Assert.That(svc.UserCanManageDeviceServices(tEng, dAssigned), Is.False);
    }

    #endregion

}
