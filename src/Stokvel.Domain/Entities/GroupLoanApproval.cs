namespace Stokvel.Domain.Entities;

public class GroupLoanApproval : BaseEntity
{
    public Guid GroupLoanId { get; set; }
    public Guid? ApproverUserId { get; set; }
    public string RequiredRole { get; set; } = string.Empty;
    public ApprovalDecision Decision { get; set; } = ApprovalDecision.Pending;
    public string? Comment { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public int StepOrder { get; set; }

    public GroupLoan? Loan { get; set; }
}
