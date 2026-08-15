namespace Stokvel.Domain.Entities;

public class MemberLoan : BaseEntity
{
    public string LoanNumber { get; set; } = string.Empty;
    public Guid GroupId { get; set; }
    public Guid MemberId { get; set; }
    public decimal Principal { get; set; }
    public decimal InterestRatePercent { get; set; }
    public decimal TotalRepayable { get; set; }
    public int RepaymentMonths { get; set; }
    public decimal MonthlyRepayment { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public LoanStatus Status { get; set; } = LoanStatus.Draft;
    public DateTime RequestedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public DateTime? DisbursedAt { get; set; }
    public Guid RequestedByUserId { get; set; }
    public Guid? DisbursementTransactionId { get; set; }
    public string? RejectionReason { get; set; }

    public StokvelGroup? Group { get; set; }
    public GroupMember? Member { get; set; }
    public ICollection<MemberLoanRepayment> Repayments { get; set; } = new List<MemberLoanRepayment>();
    public ICollection<MemberLoanApproval> Approvals { get; set; } = new List<MemberLoanApproval>();
}
