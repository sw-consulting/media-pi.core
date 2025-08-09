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

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MediaPi.Core.Authorization;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Data;

namespace MediaPi.Core.Controllers;

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

