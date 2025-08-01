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

using Microsoft.AspNetCore.Mvc;

using Fuelflux.Core.Authorization;
using Fuelflux.Core.Data;
using Fuelflux.Core.RestModels;
using Fuelflux.Core;
using Microsoft.EntityFrameworkCore;

namespace Fuelflux.Core.Controllers;

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
