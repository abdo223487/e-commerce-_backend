using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MarketplaceApi.Data;
using MarketplaceApi.DTOs;
using MarketplaceApi.Models;

namespace MarketplaceApi.Controllers
{
    [ApiController]
    [Route("api/supervisor/audit-logs")]
    [Authorize(Policy = "SupervisorOnly")]
    [Produces("application/json")]
    public class AuditLogsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public AuditLogsController(AppDbContext db)
        {
            _db = db;
        }

        private static AuditLogResponseDto Map(AuditLog a) => new()
        {
            Id = a.Id,
            ActorId = a.ActorId,
            ActorName = a.Actor?.FullName ?? string.Empty,
            ActorRole = a.Actor?.Role.ToString() ?? string.Empty,
            Action = a.Action,
            Description = a.Description,
            RelatedEntityId = a.RelatedEntityId,
            RelatedEntityType = a.RelatedEntityType,
            BeforeSnapshot = a.BeforeSnapshot,
            AfterSnapshot = a.AfterSnapshot,
            CreatedAtUtc = a.CreatedAtUtc
        };

        /// <summary>Supervisor: get all audit logs (all actions done by any admin or supervisor).</summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<AuditLogResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] AuditAction? action = null,
            [FromQuery] Guid? actorId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 50;

            var query = _db.AuditLogs.Include(a => a.Actor).AsQueryable();

            if (action.HasValue) query = query.Where(a => a.Action == action.Value);
            if (actorId.HasValue) query = query.Where(a => a.ActorId == actorId.Value);

            var logs = await query
                .OrderByDescending(a => a.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(logs.Select(Map).ToList());
        }

        /// <summary>Supervisor: get audit logs filtered by action type.</summary>
        [HttpGet("by-action/{action}")]
        [ProducesResponseType(typeof(List<AuditLogResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByAction(AuditAction action)
        {
            var logs = await _db.AuditLogs
                .Include(a => a.Actor)
                .Where(a => a.Action == action)
                .OrderByDescending(a => a.CreatedAtUtc)
                .Take(100)
                .ToListAsync();

            return Ok(logs.Select(Map).ToList());
        }
    }
}
