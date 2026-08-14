namespace Stokvel.Domain.Entities;

public class StokvelGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Code { get; set; } = string.Empty;
    public GroupStatus Status { get; set; } = GroupStatus.Draft;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public DateTimeOffset? InitialDepositCompletedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? SuspensionReason { get; set; }

    public GroupRule? Rule { get; set; }
    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    public ICollection<GroupInvitation> Invitations { get; set; } = new List<GroupInvitation>();
    public ICollection<FinancialAccount> Accounts { get; set; } = new List<FinancialAccount>();
}
