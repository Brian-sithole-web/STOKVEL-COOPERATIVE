namespace Stokvel.Domain.Entities;

public class ContributionSchedule : BaseEntity
{
    public Guid GroupId { get; set; }
    public Guid? MemberId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public ContributionStatus Status { get; set; } = ContributionStatus.Pending;
    public decimal PenaltyAmount { get; set; }
    public bool IsGroupLevel { get; set; }

    public StokvelGroup? Group { get; set; }
    public GroupMember? Member { get; set; }
    public ICollection<Contribution> Payments { get; set; } = new List<Contribution>();
}
