namespace Stokvel.Domain.Entities;

public class GroupInvitation : BaseEntity
{
    public Guid GroupId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    public DateTime ExpiresAt { get; set; }
    public Guid InvitedByUserId { get; set; }

    public StokvelGroup? Group { get; set; }
}
