namespace Stokvel.Domain.Entities;

public class MemberLoanApproval : BaseEntity
{
    public Guid MemberLoanId { get; set; }
    public Guid? ApproverUserId { get; set; }
    public string RequiredRole { get; set; } = string.Empty;
    public ApprovalDecision Decision { get; set; } = ApprovalDecision.Pending;
    public string? Comment { get; set; }
    public DateTime? DecidedAt { get; set; }
    public int StepOrder { get; set; }

    public MemberLoan? Loan { get; set; }
}
