// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Authorization;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaPi.Core.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
    public class RolesController(
        IHttpContextAccessor httpContextAccessor,
        AppDbContext db,
        ILogger<RolesController> logger) : MediaPiControllerBase(httpContextAccessor, db, logger)
    {
        // GET: api/roles
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<RoleViewItem>))]
        public async Task<ActionResult<IEnumerable<RoleViewItem>>> GetAll(CancellationToken ct = default)
        {
            var roles = await _db.Roles
                .OrderBy(r => r.Id)
                .Select(r => new RoleViewItem
                {
                    Id = r.Id,
                    RoleId = r.RoleId,
                    Name = r.Name
                })
                .ToListAsync(ct);

            return roles;
        }

        // GET: api/roles/5
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RoleViewItem))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
        public async Task<ActionResult<RoleViewItem>> GetById(int id, CancellationToken ct = default)
        {
            var role = await _db.Roles.FindAsync([id], ct);

            if (role == null)
            {
                return StatusCode(StatusCodes.Status404NotFound,
                    new ErrMessage { Msg = $"Не удалось найти роль [id={id}]" });
            }

            return new RoleViewItem
            {
                Id = role.Id,
                RoleId = role.RoleId,
                Name = role.Name
            };
        }

        // GET: api/roles/by-role-id/1
        [HttpGet("by-role-id/{roleId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RoleViewItem))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
        public async Task<ActionResult<RoleViewItem>> GetByRoleId(UserRoleConstants roleId, CancellationToken ct = default)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleId == roleId, ct);

            if (role == null)
            {
                return StatusCode(StatusCodes.Status404NotFound,
                    new ErrMessage { Msg = $"Не удалось найти роль [roleId={roleId}]" });
            }

            return new RoleViewItem
            {
                Id = role.Id,
                RoleId = role.RoleId,
                Name = role.Name
            };
        }
    }
}
