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
    public async Task<ActionResult<Status>> Status()
    {
        _logger.LogDebug("Check service status");

        // Get the last migration timestamp from the database
        string dbVersion = "Unknown";
        try
        {
            // Query the __EFMigrationsHistory table for the last applied migration
            var lastMigration = await _db.Database.GetAppliedMigrationsAsync();
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
            Msg = "Hello, world! Fuelflux Core status is fantastic!",
            AppVersion = VersionInfo.AppVersion,
            DbVersion = dbVersion,
        };

        _logger.LogDebug("Check service status returning:\n{status}", status);
        return Ok(status);
    }
}
