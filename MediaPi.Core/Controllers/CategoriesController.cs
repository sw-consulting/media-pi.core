// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Authorization;
using MediaPi.Core.Data;
using MediaPi.Core.Extensions;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaPi.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
public class CategoriesController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<CategoriesController> logger) : MediaPiControllerBase(httpContextAccessor, db, logger)
{
    private const string CategoryTitleIndex = "IX_categories_title";
    private const string VideoCategoryForeignKey = "FK_videos_categories_category_id";
    private const string SubscriptionCategoryForeignKey = "FK_subscriptions_categories_category_id";

    // GET: api/categories
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<CategoryViewItem>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<CategoryViewItem>>> GetAll(CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null || !(user.IsAdministrator() || user.IsManager())) return _403();

        var categories = await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Title)
            .ToListAsync(ct);

        return categories.Select(c => c.ToViewItem()).ToList();
    }

    // GET: api/categories/{id}
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CategoryViewItem))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<CategoryViewItem>> GetCategory(int id, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null || !(user.IsAdministrator() || user.IsManager())) return _403();

        var category = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category == null) return _404Category(id);

        return category.ToViewItem();
    }

    // POST: api/categories
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Reference))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<ActionResult<Reference>> CreateCategory(CategoryCreateItem item, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null || !user.IsAdministrator()) return _403();

        var category = new Category
        {
            Title = item.Title,
            Free = item.Free
        };

        _db.Categories.Add(category);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsCategoryTitleConstraint(ex))
        {
            return _409Category(item.Title);
        }

        return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, new Reference { Id = category.Id });
    }

    // PUT: api/categories/{id}
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdateCategory(int id, CategoryUpdateItem item, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null || !user.IsAdministrator()) return _403();

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category == null) return _404Category(id);

        category.UpdateFrom(item);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsCategoryTitleConstraint(ex))
        {
            return _409Category(category.Title);
        }

        return NoContent();
    }

    // DELETE: api/categories/{id}
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null || !user.IsAdministrator()) return _403();

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category == null) return _404Category(id);

        _db.Categories.Remove(category);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsCategoryReferenceConstraint(ex))
        {
            return _409CategoryInUse(id);
        }

        return NoContent();
    }

    private static bool IsCategoryTitleConstraint(Exception ex) =>
        ContainsExceptionDetail(ex, CategoryTitleIndex);

    private static bool IsCategoryReferenceConstraint(Exception ex) =>
        ContainsExceptionDetail(ex, VideoCategoryForeignKey) ||
        ContainsExceptionDetail(ex, SubscriptionCategoryForeignKey);

    private static bool ContainsExceptionDetail(Exception ex, string detail)
    {
        for (Exception? current = ex; current != null; current = current.InnerException)
        {
            if (current.Message.Contains(detail, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var constraintName = current.GetType().GetProperty("ConstraintName")?.GetValue(current)?.ToString();
            if (constraintName?.Equals(detail, StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }
        }

        return false;
    }
}
