// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using Microsoft.AspNetCore.Mvc;

using MediaPi.Core.Authorization;
using MediaPi.Core.Data;
using MediaPi.Core.RestModels;
using MediaPi.Core;
using Microsoft.EntityFrameworkCore;

namespace MediaPi.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class StatusController(
    AppDbContext db,
    ILogger<StatusController> logger) : FuelfluxControllerPreBase(db, logger)
{
    // GET: api/auth/status
    // Checks service status
    [HttpGet("status")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json", Type = typeof(Status))]
    public async Task<ActionResult<Status>> Status(CancellationToken ct = default)
    {
        _logger.LogDebug("Check service status");

        // Get the last migration timestamp from the database
        string dbVersion = "Unknown";
        try
        {
            // Query the __EFMigrationsHistory table for the last applied migration
            var lastMigration = await _db.Database.GetAppliedMigrationsAsync(ct);
            dbVersion = lastMigration.LastOrDefault() ?? "00000000000000";
            // Truncate dbVersion up to the first '_' if present
            if (dbVersion.Contains('_'))
            {
                dbVersion = dbVersion[..dbVersion.IndexOf('_')];
            }

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving migration history");
            dbVersion = "00000000000000";
        }

        Status status = new()
        {
            Msg = "Hello, world! MediaPi Core status is fantastic!",
            AppVersion = VersionInfo.AppVersion,
            DbVersion = dbVersion,
        };

        _logger.LogDebug("Check service status returning:\n{status}", status);
        return Ok(status);
    }
}
