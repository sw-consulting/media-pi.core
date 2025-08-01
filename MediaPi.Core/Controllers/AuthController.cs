// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Fuelflux core application
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
using Microsoft.EntityFrameworkCore;

using Fuelflux.Core.Authorization;
using Fuelflux.Core.Models;
using Fuelflux.Core.RestModels;
using Fuelflux.Core.Data;

namespace Fuelflux.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AuthController(
    AppDbContext db, 
    IJwtUtils jwtUtils,
    ILogger<AuthController> logger) : FuelfluxControllerPreBase(db, logger)
{
    private readonly IJwtUtils _jwtUtils = jwtUtils;

    // POST: api/auth/login
    [AllowAnonymous]
    [HttpPost("login")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserViewItemWithJWT))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
    public async Task<ActionResult<UserViewItem>> Login(Credentials crd)
    {
        _logger.LogDebug("Login attempt for {email}", crd.Email);

        User? user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Where(u => u.Email.ToLower() == crd.Email.ToLower())
            .SingleOrDefaultAsync();

        if (user == null) return _401();

        if (!BCrypt.Net.BCrypt.Verify(crd.Password, user.Password)) return _401();
        if (!user.HasAnyRole()) return _403();

        UserViewItemWithJWT userViewItem = new(user)
        {
            Token = _jwtUtils.GenerateJwtToken(user),
        };

        _logger.LogDebug("Login returning\n{res}", userViewItem.ToString());
        return userViewItem;
    }

    // GET: api/auth/check
    // Checks authorization status
    [HttpGet("check")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
    public IActionResult Check()
    {
        _logger.LogDebug("Check authorization status");
        return NoContent();
    }

}

