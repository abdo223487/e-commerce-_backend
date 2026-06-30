using System.Text.Json;
using MarketplaceApi.Data;
using MarketplaceApi.Models;

namespace MarketplaceApi.Services
{
    public interface IAuditService
    {
        Task LogAsync(Guid actorId, AuditAction action, string description,
            Guid? relatedEntityId = null, string? relatedEntityType = null,
            object? before = null, object? after = null);
    }

    public class AuditService : IAuditService
    {
        private readonly AppDbContext _db;
        private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = false };

        public AuditService(AppDbContext db) => _db = db;

        public async Task LogAsync(Guid actorId, AuditAction action, string description,
            Guid? relatedEntityId = null, string? relatedEntityType = null,
            object? before = null, object? after = null)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                ActorId = actorId,
                Action = action,
                Description = description,
                RelatedEntityId = relatedEntityId,
                RelatedEntityType = relatedEntityType,
                BeforeSnapshot = before is null ? null : JsonSerializer.Serialize(before, _jsonOpts),
                AfterSnapshot = after is null ? null : JsonSerializer.Serialize(after, _jsonOpts)
            });

            await _db.SaveChangesAsync();
        }
    }
}
