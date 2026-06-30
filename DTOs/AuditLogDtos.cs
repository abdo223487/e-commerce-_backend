using MarketplaceApi.Models;

namespace MarketplaceApi.DTOs
{
    public class AuditLogResponseDto
    {
        public Guid Id { get; set; }
        public Guid ActorId { get; set; }
        public string ActorName { get; set; } = string.Empty;
        public string ActorRole { get; set; } = string.Empty;
        public AuditAction Action { get; set; }
        public string ActionName => Action.ToString();
        public string Description { get; set; } = string.Empty;
        public Guid? RelatedEntityId { get; set; }
        public string? RelatedEntityType { get; set; }
        public string? BeforeSnapshot { get; set; }
        public string? AfterSnapshot { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
