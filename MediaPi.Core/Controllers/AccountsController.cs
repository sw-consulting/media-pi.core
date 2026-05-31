// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Authorization;
using MediaPi.Core.Data;
using MediaPi.Core.Extensions;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services.Interfaces;
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
    IPlaylistAccessService playlistAccessService,
    ISubscriptionTimeService subscriptionTimeService,
    AppDbContext db,
    ILogger<AccountsController> logger) : MediaPiControllerBase(httpContextAccessor, db, logger)
{
    private readonly IUserInformationService _userInformationService = userInformationService;
    private readonly IPlaylistAccessService _playlistAccessService = playlistAccessService;
    private readonly ISubscriptionTimeService _subscriptionTimeService = subscriptionTimeService;

    // GET: api/accounts
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<AccountViewItem>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<AccountViewItem>>> GetAll(CancellationToken ct = default)
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

        var accounts = await query.ToListAsync(ct);
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
    public async Task<ActionResult<AccountViewItem>> GetAccount(int id, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var account = await _db.Accounts.Include(a => a.UserAccounts).FirstOrDefaultAsync(a => a.Id == id, ct);
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
    public async Task<ActionResult<IEnumerable<AccountViewItem>>> GetAccountsByManager(int userId, CancellationToken ct = default)
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
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

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

    // GET: api/accounts/{id}/subscriptions
    [HttpGet("{id}/subscriptions")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AccountSubscriptionsViewItem))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<AccountSubscriptionsViewItem>> GetSubscriptions(int id, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var account = await _db.Accounts
            .AsNoTracking()
            .Include(a => a.UserAccounts)
            .Include(a => a.Subscriptions)
                .ThenInclude(s => s.Category)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account == null) return _404Account(id);

        if (!user.IsAdministrator() && !_userInformationService.ManagerOwnsAccount(user, account)) return _403();

        var subscribedCategoryIds = account.Subscriptions.Select(s => s.CategoryId).ToHashSet();
        var availableCategories = await _db.Categories
            .AsNoTracking()
            .Where(c => !subscribedCategoryIds.Contains(c.Id))
            .OrderBy(c => c.Title)
            .ToListAsync(ct);

        return new AccountSubscriptionsViewItem
        {
            Subscriptions = account.Subscriptions
                .OrderBy(s => s.Category.Title)
                .Select(s => new SubscriptionViewItem(s, _subscriptionTimeService))
                .ToList(),
            AvailableCategories = availableCategories.Select(c => c.ToViewItem()).ToList()
        };
    }

    // PUT: api/accounts/{id}/subscriptions/{categoryId}
    [HttpPut("{id}/subscriptions/{categoryId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(PlaylistAccessImpact))]
    public async Task<IActionResult> UpsertSubscription(
        int id,
        int categoryId,
        SubscriptionUpsertItem item,
        CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null || !user.IsAdministrator()) return _403();
        if (item.EndDate < item.StartDate) return _400SubscriptionDateRangeInvalid();

        var accountExists = await _db.Accounts.AsNoTracking().AnyAsync(a => a.Id == id, ct);
        if (!accountExists) return _404Account(id);

        var categoryExists = await _db.Categories.AsNoTracking().AnyAsync(c => c.Id == categoryId, ct);
        if (!categoryExists) return _404Category(categoryId);

        var startUtc = _subscriptionTimeService.ToUtcStart(item.StartDate);
        var endUtc = _subscriptionTimeService.ToUtcEnd(item.EndDate);

        var impact = await _playlistAccessService.BuildSubscriptionChangeImpactAsync(id, categoryId, startUtc, endUtc, ct);
        if (impact.HasImpact && !item.ForcePlaylistCleanup)
        {
            return StatusCode(StatusCodes.Status409Conflict, impact);
        }

        var subscription = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.AccountId == id && s.CategoryId == categoryId, ct);

        if (subscription == null)
        {
            subscription = new Subscription
            {
                AccountId = id,
                CategoryId = categoryId,
                StartTime = startUtc,
                EndTime = endUtc
            };
            _db.Subscriptions.Add(subscription);
        }
        else
        {
            subscription.StartTime = startUtc;
            subscription.EndTime = endUtc;
        }

        if (item.ForcePlaylistCleanup && impact.HasImpact)
        {
            await _playlistAccessService.RemovePlaylistItemsAsync(impact.VideoPlaylistIds, ct);
        }
        else
        {
            await _db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    // POST: api/accounts
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Reference))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<ActionResult<Reference>> AddAccount(AccountCreateItem item, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null || !user.IsAdministrator()) return _403();

        if (await _db.Accounts.AnyAsync(a => a.Name == item.Name, ct)) return _409Account(item.Name);

        var account = new Account { Name = item.Name };
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync(ct);

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
            await _db.SaveChangesAsync(ct);
        }

        return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, new Reference { Id = account.Id });
    }

    // PUT: api/accounts/{id}
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdateAccount(int id, AccountUpdateItem item, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null || !user.IsAdministrator()) return _403();

        var account = await _db.Accounts.Include(a => a.UserAccounts).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account == null) return _404Account(id);

        if (item.Name != null && await _db.Accounts.AnyAsync(a => a.Name == item.Name && a.Id != id, ct))
        {
            return _409Account(item.Name);
        }

        account.UpdateFrom(item);
        await _db.SaveChangesAsync(ct);

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
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // DELETE: api/accounts/{id}
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteAccount(int id, CancellationToken ct = default)
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
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account == null) return _404Account(id);

        _db.DeviceGroups.RemoveRange(account.DeviceGroups);
        _db.Videos.RemoveRange(account.Videos);
        _db.Playlists.RemoveRange(account.Playlists);
        _db.Subscriptions.RemoveRange(account.Subscriptions);
        _db.UserAccounts.RemoveRange(account.UserAccounts);

        _db.Accounts.Remove(account);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
