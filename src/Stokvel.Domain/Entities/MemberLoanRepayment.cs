namespace Stokvel.Domain.Entities;

public class MemberLoanRepayment : BaseEntity
{
    public Guid MemberLoanId { get; set; }
    public int InstalmentNumber { get; set; }
    public DateTime DueDate { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public DateTime? PaidDate { get; set; }
    public ContributionStatus Status { get; set; } = ContributionStatus.Pending;

    public MemberLoan? Loan { get; set; }
}
