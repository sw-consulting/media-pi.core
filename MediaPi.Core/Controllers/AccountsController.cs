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
//
// This file is a part of Media Pi backend application

using MediaPi.Core.Authorization;
using MediaPi.Core.Data;
using MediaPi.Core.Extensions;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaPi.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
public class AccountsController(
    IHttpContextAccessor httpContextAccessor,
    IUserInformationService userInformationService,
    AppDbContext db,
    ILogger<AccountsController> logger) : MediaPiControllerBase(httpContextAccessor, db, logger)
{
    private readonly IUserInformationService _userInformationService = userInformationService;

    // GET: api/accounts
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<AccountViewItem>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<AccountViewItem>>> GetAll()
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        IQueryable<Account> query = _db.Accounts.Include(a => a.UserAccounts);

        if (user.IsAdministrator())
        {
            // all accounts
        }
        else if (user.IsManager())
        {
            var accountIds = _userInformationService.GetUserAccountIds(user);
            query = query.Where(a => accountIds.Contains(a.Id));
        }
        else if (user.IsEngineer())
        {
            // Installation Engineers can see all accounts but with limited information
        }
        else
        {
            return _403();
        }

        var accounts = await query.ToListAsync();
        var result = accounts.Select(a => {
            var viewItem = a.ToViewItem();
            if (user.IsAdministrator())
            {
                viewItem.UserIds = [.. a.UserAccounts.Select(ua => ua.UserId)];
            }
            else if (user.IsEngineer())
            {
                // For Installation Engineers, only return Id and Name, clear UserIds
                viewItem.UserIds = [];
            }
            return viewItem;
        }).ToList();
        return result;
    }

    // GET: api/accounts/{id}
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AccountViewItem))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<AccountViewItem>> GetAccount(int id)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var account = await _db.Accounts.Include(a => a.UserAccounts).FirstOrDefaultAsync(a => a.Id == id);
        if (account == null) return _404Account(id);

        if (user.IsAdministrator() || _userInformationService.ManagerOwnsAccount(user, account))
        {
            var viewItem = account.ToViewItem();
            if (user.IsAdministrator())
                viewItem.UserIds = [.. account.UserAccounts.Select(ua => ua.UserId)];
            return viewItem;
        }

        return _403();
    }

    // GET: api/accounts/by-manager/{userId}
    [HttpGet("by-manager/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<AccountViewItem>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<AccountViewItem>>> GetAccountsByManager(int userId)
    {
        _logger.LogDebug("GetAccountsByManager for userId={userId}", userId);

        var currentUser = await CurrentUser();
        if (currentUser == null) return _403();

        // Only admin or the user themselves can access this endpoint
        if (!currentUser.IsAdministrator() && currentUser.Id != userId)
        {
            _logger.LogDebug("GetAccountsByManager returning '403 Forbidden' - insufficient permissions");
            return _403();
        }

        // Check if the user exists
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.UserAccounts)
                .ThenInclude(ua => ua.Account)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            _logger.LogDebug("GetAccountsByManager returning '404 Not Found'");
            return _404User(userId);
        }

        // Check if user has AccountManager role
        var hasManagerRole = user.UserRoles.Any(ur => ur.Role!.RoleId == UserRoleConstants.AccountManager);
        if (!hasManagerRole)
        {
            _logger.LogDebug("GetAccountsByManager returning empty array - user does not have AccountManager role");
            return new List<AccountViewItem>();
        }

        // Get accounts managed by this user
        var accounts = user.UserAccounts
            .Select(ua => ua.Account.ToViewItem())
            .ToList();

        _logger.LogDebug("GetAccountsByManager returning {count} accounts", accounts.Count);
        return accounts;
    }

    // POST: api/accounts
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Reference))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<ActionResult<Reference>> AddAccount(AccountCreateItem item)
    {
        var user = await CurrentUser();
        if (user == null || !user.IsAdministrator()) return _403();

        if (await _db.Accounts.AnyAsync(a => a.Name == item.Name)) return _409Account(item.Name);

        var account = new Account { Name = item.Name };
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();

        // Handle UserIds for AccountManager role
        if (item.UserIds != null && item.UserIds.Count > 0)
        {
            var managerIds = _db.Users
                .Include(u => u.UserRoles)
                .Where(u => item.UserIds.Contains(u.Id) && u.UserRoles.Any(ur => ur.Role.RoleId == UserRoleConstants.AccountManager))
                .Select(u => u.Id)
                .ToList();
            foreach (var userId in managerIds)
            {
                _db.UserAccounts.Add(new UserAccount { UserId = userId, AccountId = account.Id });
            }
            await _db.SaveChangesAsync();
        }

        return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, new Reference { Id = account.Id });
    }

    // PUT: api/accounts/{id}
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdateAccount(int id, AccountUpdateItem item)
    {
        var user = await CurrentUser();
        if (user == null || !user.IsAdministrator()) return _403();

        var account = await _db.Accounts.Include(a => a.UserAccounts).FirstOrDefaultAsync(a => a.Id == id);
        if (account == null) return _404Account(id);

        if (item.Name != null && await _db.Accounts.AnyAsync(a => a.Name == item.Name && a.Id != id))
        {
            return _409Account(item.Name);
        }

        account.UpdateFrom(item);
        _db.Entry(account).State = EntityState.Modified;
        await _db.SaveChangesAsync();

        // Handle UserIds for AccountManager role
        var existingUserAccounts = _db.UserAccounts.Where(ua => ua.AccountId == id);
        _db.UserAccounts.RemoveRange(existingUserAccounts);
        if (item.UserIds != null && item.UserIds.Count > 0)
        {
            var managerIds = _db.Users
                .Include(u => u.UserRoles)
                .Where(u => item.UserIds.Contains(u.Id) && u.UserRoles.Any(ur => ur.Role.RoleId == UserRoleConstants.AccountManager))
                .Select(u => u.Id)
                .ToList();
            foreach (var userId in managerIds)
            {
                _db.UserAccounts.Add(new UserAccount { UserId = userId, AccountId = id });
            }
        }
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: api/accounts/{id}
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteAccount(int id)
    {
        var user = await CurrentUser();
        if (user == null || !user.IsAdministrator()) return _403();

        var account = await _db.Accounts
            .Include(a => a.Devices)
            .Include(a => a.DeviceGroups)
            .Include(a => a.Videos)
            .Include(a => a.Playlists)
            .Include(a => a.Subscriptions)
            .Include(a => a.UserAccounts)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (account == null) return _404Account(id);

        _db.DeviceGroups.RemoveRange(account.DeviceGroups);
        _db.Videos.RemoveRange(account.Videos);
        _db.Playlists.RemoveRange(account.Playlists);
        _db.Subscriptions.RemoveRange(account.Subscriptions);
        _db.UserAccounts.RemoveRange(account.UserAccounts);

        _db.Accounts.Remove(account);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

