namespace Stokvel.Domain.Entities;

public class Contribution : BaseEntity
{
    public Guid GroupId { get; set; }
    public Guid? MemberId { get; set; }
    public Guid? ScheduleId { get; set; }
    public decimal Amount { get; set; }
    public ContributionKind Kind { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public string? PaymentReference { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Eft;
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public Guid? FinancialTransactionId { get; set; }
    public Guid RecordedByUserId { get; set; }

    public StokvelGroup? Group { get; set; }
    public GroupMember? Member { get; set; }
    public ContributionSchedule? Schedule { get; set; }
}
