namespace Stokvel.Domain;

public enum GroupStatus
{
    Draft = 0,
    AwaitingMembers = 1,
    AwaitingInitialDeposit = 2,
    PendingApproval = 3,
    Active = 4,
    Suspended = 5,
    Closed = 6
}

public enum GroupRole
{
    Member = 0,
    Secretary = 1,
    Treasurer = 2,
    Chairperson = 3,
    StokvelAdministrator = 4
}

public enum MembershipStatus
{
    Invited = 0,
    Active = 1,
    Suspended = 2,
    Left = 3
}

public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Expired = 2,
    Cancelled = 3
}

public enum ContributionStatus
{
    Pending = 0,
    Paid = 1,
    PartiallyPaid = 2,
    Overdue = 3,
    Waived = 4,
    Cancelled = 5
}

public enum ContributionKind
{
    InitialDeposit = 0,
    MonthlyInstalment = 1,
    AdditionalDeposit = 2
}

public enum AccountType
{
    GroupCapital = 0,
    MemberContribution = 1,
    LoanReceivable = 2,
    CooperativePool = 3
}

public enum TransactionType
{
    InitialDeposit = 0,
    MonthlyInstalment = 1,
    AdditionalDeposit = 2,
    LoanRepayment = 3,
    InterestReceived = 4,
    PenaltyReceived = 5,
    OtherIncome = 6,
    GroupLoanDisbursement = 7,
    MemberLoanDisbursement = 8,
    GroupWithdrawal = 9,
    ApprovedExpense = 10,
    Refund = 11,
    OtherExpense = 12,
    Reversal = 13,
    Adjustment = 14,
    Correction = 15
}

public enum TransactionDirection
{
    Inflow = 0,
    Outflow = 1
}

public enum TransactionStatus
{
    Pending = 0,
    Confirmed = 1,
    Reversed = 2,
    Adjustment = 3
}

public enum PaymentMethod
{
    Eft = 0,
    Cash = 1,
    Card = 2,
    Other = 3
}

public enum CardPaymentStatus
{
    Created = 0,
    AwaitingBank = 1,
    Succeeded = 2,
    Failed = 3,
    Expired = 4
}

public enum LoanStatus
{
    Draft = 0,
    Submitted = 1,
    UnderReview = 2,
    Approved = 3,
    Rejected = 4,
    Disbursed = 5,
    Active = 6,
    PartiallyRepaid = 7,
    FullyRepaid = 8,
    Defaulted = 9,
    Cancelled = 10
}

public enum LendingSource
{
    GroupSavingsPool = 1,
    CooperativeLendingPool = 2,
    Both = 3
}

public enum ApprovalDecision
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public enum LoanApprovalWorkflow
{
    StokvelAdminThenPlatformAdmin = 0,
    TreasurerThenPlatformAdmin = 1,
    CommitteeThenPlatformAdmin = 2,
    PlatformAdminOnly = 3
}

public enum NotificationType
{
    MembersReached = 0,
    InitialDepositDue = 1,
    InitialDepositConfirmed = 2,
    InstalmentDue = 3,
    InstalmentOverdue = 4,
    GroupLoanSubmitted = 5,
    GroupLoanApproved = 6,
    GroupLoanRejected = 7,
    MemberLoanSubmitted = 8,
    MemberLoanApproved = 9,
    MemberLoanRejected = 10,
    RepaymentDue = 11,
    RepaymentOverdue = 12,
    GroupActivated = 13,
    Invitation = 14,
    General = 15
}

public static class SystemRoles
{
    public const string PlatformAdmin = "PLATFORM_ADMIN";
    public const string User = "USER";
}

public static class BusinessConstants
{
    public const int MinimumMembers = 5;
    public const string Currency = "ZAR";
}
