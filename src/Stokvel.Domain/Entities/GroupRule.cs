namespace Stokvel.Domain.Entities;

public class GroupRule : BaseEntity
{
    public Guid GroupId { get; set; }

    public decimal InitialDepositAmount { get; set; }
    public decimal MonthlyInstalmentAmount { get; set; }
    public int InstalmentDueDay { get; set; } = 25;
    public int GracePeriodDays { get; set; } = 5;
    public decimal LatePenaltyAmount { get; set; }
    public bool AllowPartialPayments { get; set; } = true;
    public bool AllowAdditionalPayments { get; set; } = true;

    public bool MemberLoansEnabled { get; set; } = true;
    public decimal MaxGroupBorrowingPercentOfSavings { get; set; } = 80m;
    public decimal MemberLoanMultiplier { get; set; } = 2m;
    public int MinimumMembers { get; set; } = BusinessConstants.MinimumMembers;
    public int MinimumGroupAgeDays { get; set; }
    public int MinimumMonthlyInstalments { get; set; }
    public decimal MinimumSavingsBalance { get; set; }
    public bool RequireNoOverdueInstalments { get; set; } = true;
    public bool RequireNoDefaultedLoan { get; set; } = true;

    public LendingSource AllowedLendingSources { get; set; } = LendingSource.Both;
    public LoanApprovalWorkflow ApprovalWorkflow { get; set; } = LoanApprovalWorkflow.StokvelAdminThenPlatformAdmin;
    public decimal DefaultInterestRatePercent { get; set; } = 10m;

    public StokvelGroup? Group { get; set; }
}
