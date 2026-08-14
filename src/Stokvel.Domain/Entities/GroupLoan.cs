namespace Stokvel.Domain.Entities;

public class GroupLoan : BaseEntity
{
    public string LoanNumber { get; set; } = string.Empty;
    public Guid GroupId { get; set; }
    public decimal Principal { get; set; }
    public decimal InterestRatePercent { get; set; }
    public decimal TotalRepayable { get; set; }
    public int RepaymentMonths { get; set; }
    public decimal MonthlyRepayment { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public LoanStatus Status { get; set; } = LoanStatus.Draft;
    public LendingSource Source { get; set; } = LendingSource.GroupSavingsPool;
    public DateTimeOffset RequestedDate { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? DisbursedAt { get; set; }
    public Guid RequestedByUserId { get; set; }
    public Guid? DisbursementTransactionId { get; set; }
    public string? RejectionReason { get; set; }

    public StokvelGroup? Group { get; set; }
    public ICollection<GroupLoanRepayment> Repayments { get; set; } = new List<GroupLoanRepayment>();
    public ICollection<GroupLoanApproval> Approvals { get; set; } = new List<GroupLoanApproval>();
}
