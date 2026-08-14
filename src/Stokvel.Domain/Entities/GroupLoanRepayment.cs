namespace Stokvel.Domain.Entities;

public class GroupLoanRepayment : BaseEntity
{
    public Guid GroupLoanId { get; set; }
    public int InstalmentNumber { get; set; }
    public DateTime DueDate { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public DateTime? PaidDate { get; set; }
    public ContributionStatus Status { get; set; } = ContributionStatus.Pending;

    public GroupLoan? Loan { get; set; }
}
