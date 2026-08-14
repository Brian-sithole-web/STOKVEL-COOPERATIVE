namespace Stokvel.Domain.Entities;

public class AuditLog : BaseEntity
{
    public Guid? GroupId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
}
