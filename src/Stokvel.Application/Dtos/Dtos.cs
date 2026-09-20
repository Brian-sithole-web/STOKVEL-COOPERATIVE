using Stokvel.Domain;

namespace Stokvel.Application.Dtos;

public record SetupStatusDto(bool IsConfigured, bool RequiresSetup);

public record InitializeSetupRequest(string FullName, string Email, string Password);

public record RegisterRequest(string FullName, string Email, string Password);

public record LoginRequest(string Email, string Password);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record ForgotPasswordRequest(string Email, string? ClientOrigin = null);

public record ResetPasswordRequest(string Email, string Token, string Password);

public record ForgotPasswordResultDto(string Message, bool EmailSent, string? ResetUrl);

public record AuthResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    string Email,
    string FullName,
    bool IsPlatformAdmin,
    bool MustChangePassword,
    IReadOnlyList<GroupMembershipDto> Memberships);

public record GroupMembershipDto(Guid GroupId, string GroupName, Guid MemberId, GroupRole Role, MembershipStatus Status);

public record CreateGroupRequest(
    string Name,
    string? Description,
    decimal InitialDepositAmount,
    decimal MonthlyInstalmentAmount,
    int InstalmentDueDay,
    int GracePeriodDays,
    decimal LatePenaltyAmount,
    bool AllowPartialPayments,
    bool AllowAdditionalPayments,
    bool MemberLoansEnabled,
    decimal MaxGroupBorrowingPercentOfSavings,
    decimal MemberLoanMultiplier,
    LendingSource AllowedLendingSources,
    LoanApprovalWorkflow ApprovalWorkflow,
    decimal DefaultInterestRatePercent);

public record UpdateGroupRulesRequest(
    decimal InitialDepositAmount,
    decimal MonthlyInstalmentAmount,
    int InstalmentDueDay,
    int GracePeriodDays,
    decimal LatePenaltyAmount,
    bool AllowPartialPayments,
    bool AllowAdditionalPayments,
    bool MemberLoansEnabled,
    decimal MaxGroupBorrowingPercentOfSavings,
    decimal MemberLoanMultiplier,
    int MinimumGroupAgeDays,
    int MinimumMonthlyInstalments,
    decimal MinimumSavingsBalance,
    bool RequireNoOverdueInstalments,
    bool RequireNoDefaultedLoan,
    LendingSource AllowedLendingSources,
    LoanApprovalWorkflow ApprovalWorkflow,
    decimal DefaultInterestRatePercent);

public record InviteMemberRequest(string Email, string? ClientOrigin = null);
public record InviteResultDto(string Email, string Token, DateTimeOffset ExpiresAt, bool EmailSent, string Message);
public record InvitePreviewDto(string Email, string GroupName, DateTimeOffset ExpiresAt, Guid InvitationId);

public record AcceptInviteRequest(string Token, string FullName, string Password);

public record GroupListItemDto(
    Guid Id,
    string Name,
    string Code,
    GroupStatus Status,
    string AdministratorName,
    int MemberCount,
    decimal InitialDeposit,
    decimal MonthlyInstalment,
    decimal TotalSavings,
    decimal OutstandingLoan,
    string LoanStatus,
    DateTimeOffset DateCreated);

public record GroupDetailDto(
    Guid Id,
    string Name,
    string? Description,
    string Code,
    GroupStatus Status,
    DateTimeOffset RegistrationDate,
    string AdministratorName,
    int MemberCount,
    GroupRuleDto Rules,
    GroupFinancialSummaryDto Financials);

public record GroupRuleDto(
    decimal InitialDepositAmount,
    decimal MonthlyInstalmentAmount,
    int InstalmentDueDay,
    int GracePeriodDays,
    decimal LatePenaltyAmount,
    bool AllowPartialPayments,
    bool AllowAdditionalPayments,
    bool MemberLoansEnabled,
    decimal MaxGroupBorrowingPercentOfSavings,
    decimal MemberLoanMultiplier,
    int MinimumMembers,
    int MinimumGroupAgeDays,
    int MinimumMonthlyInstalments,
    decimal MinimumSavingsBalance,
    bool RequireNoOverdueInstalments,
    bool RequireNoDefaultedLoan,
    LendingSource AllowedLendingSources,
    LoanApprovalWorkflow ApprovalWorkflow,
    decimal DefaultInterestRatePercent);

public record GroupFinancialSummaryDto(
    decimal InitialDeposit,
    decimal TotalMonthlyInstalments,
    decimal AdditionalDeposits,
    decimal CurrentSavings,
    decimal AvailableFunds,
    decimal LoansDisbursed,
    decimal OutstandingLoans,
    decimal Repayments,
    decimal Interest,
    decimal Penalties,
    decimal Expenses);

public record MemberDto(
    Guid Id,
    Guid UserId,
    string FullName,
    string Email,
    GroupRole Role,
    MembershipStatus Status,
    DateTimeOffset JoinedAt,
    decimal Contributions,
    decimal OutstandingInstalments,
    decimal LoanBalance);

public record RecordContributionRequest(
    Guid? MemberId,
    Guid? ScheduleId,
    decimal Amount,
    ContributionKind Kind,
    DateTimeOffset PaymentDate,
    string? PaymentReference,
    PaymentMethod PaymentMethod);

public record ContributionDto(
    Guid Id,
    Guid GroupId,
    Guid? MemberId,
    string? MemberName,
    decimal Amount,
    ContributionKind Kind,
    DateTimeOffset PaymentDate,
    string? PaymentReference,
    PaymentMethod PaymentMethod,
    TransactionStatus Status,
    string? TransactionNumber);

public record InstalmentDto(
    Guid Id,
    Guid GroupId,
    Guid? MemberId,
    string? MemberName,
    int Year,
    int Month,
    decimal AmountDue,
    decimal AmountPaid,
    decimal Outstanding,
    DateTime DueDate,
    DateTime? PaidDate,
    ContributionStatus Status,
    decimal PenaltyAmount,
    bool IsGroupLevel);

public record TransactionDto(
    Guid Id,
    string TransactionNumber,
    Guid GroupId,
    Guid? MemberId,
    TransactionType Type,
    TransactionDirection Direction,
    decimal Amount,
    DateTimeOffset TransactionDate,
    TransactionStatus Status,
    string? PaymentReference,
    PaymentMethod? PaymentMethod,
    string? Description,
    Guid RecordedByUserId);

public record LedgerSummaryDto(
    decimal OpeningBalance,
    decimal MoneyReceived,
    decimal MoneyPaidOut,
    decimal ClosingBalance,
    IReadOnlyList<TransactionDto> Transactions);

public record CreateGroupLoanRequest(
    decimal Principal,
    decimal InterestRatePercent,
    int RepaymentMonths,
    string Purpose,
    LendingSource Source);

public record CreateMemberLoanRequest(
    decimal Principal,
    decimal InterestRatePercent,
    int RepaymentMonths,
    string Purpose);

public record DecideLoanRequest(bool Approve, string? Comment);

public record RecordRepaymentRequest(decimal Amount, DateTimeOffset PaymentDate, string? PaymentReference, PaymentMethod PaymentMethod);

public record GroupLoanDto(
    Guid Id,
    string LoanNumber,
    Guid GroupId,
    string GroupName,
    decimal Principal,
    decimal InterestRatePercent,
    decimal TotalRepayable,
    int RepaymentMonths,
    decimal MonthlyRepayment,
    string Purpose,
    LoanStatus Status,
    LendingSource Source,
    DateTimeOffset RequestedDate,
    decimal AmountPaid,
    decimal Outstanding,
    DateTime? NextDueDate,
    decimal? NextInstalment);

public record MemberLoanDto(
    Guid Id,
    string LoanNumber,
    Guid GroupId,
    Guid MemberId,
    string MemberName,
    decimal Principal,
    decimal InterestRatePercent,
    decimal TotalRepayable,
    int RepaymentMonths,
    decimal MonthlyRepayment,
    string Purpose,
    LoanStatus Status,
    DateTimeOffset RequestedDate,
    decimal AmountPaid,
    decimal Outstanding,
    DateTime? NextDueDate,
    decimal? NextInstalment);

public record LoanRepaymentDto(
    Guid Id,
    int InstalmentNumber,
    DateTime DueDate,
    decimal AmountDue,
    decimal AmountPaid,
    DateTime? PaidDate,
    ContributionStatus Status);

public record EligibilityResultDto(bool Eligible, decimal MaxAmount, IReadOnlyList<string> Reasons);

public record PlatformDashboardDto(
    int TotalStokvelGroups,
    int ActiveStokvelGroups,
    int PendingGroups,
    int TotalMembers,
    decimal TotalInitialDeposits,
    decimal TotalMonthlyInstalments,
    decimal TotalCooperativeSavings,
    decimal TotalGroupLoans,
    decimal TotalMemberLoans,
    decimal OutstandingLoans,
    decimal OverdueLoans,
    decimal TotalRepayments);

public record GroupDashboardDto(
    Guid GroupId,
    string GroupName,
    GroupStatus Status,
    decimal GroupBalance,
    decimal InitialDeposit,
    decimal MonthlyInstalment,
    decimal TotalContributions,
    decimal OutstandingInstalments,
    decimal AvailableFunds,
    decimal OutstandingLoans,
    DateTime? NextInstalmentDue,
    int MemberCount);

public record MemberDashboardDto(
    Guid GroupId,
    string GroupName,
    decimal MyTotalContributions,
    decimal MyMonthlyInstalment,
    decimal MyOutstandingInstalments,
    decimal MyLoan,
    decimal MyOutstandingLoan,
    DateTime? MyNextPayment,
    GroupRole Role);

public record NotificationDto(
    Guid Id,
    string Title,
    string Message,
    NotificationType Type,
    bool IsRead,
    DateTimeOffset CreatedAt,
    Guid? GroupId,
    Guid? InvitationId);

public record JoinInviteResult(AuthResponse Auth, FirstPaymentDto FirstPayment);

public record FirstPaymentDto(
    Guid GroupId,
    string GroupName,
    Guid MemberId,
    decimal InitialDepositAmount,
    decimal MonthlyInstalmentAmount,
    decimal SuggestedAmount,
    ContributionKind SuggestedKind,
    bool InitialDepositOutstanding);

public record StartCardPaymentRequest(ContributionKind Kind, decimal Amount);

public record CardPaymentDto(
    Guid PaymentId,
    Guid GroupId,
    Guid ContributionId,
    string GroupName,
    decimal Amount,
    ContributionKind Kind,
    CardPaymentStatus Status,
    string CheckoutPath,
    string ChallengePath,
    DateTimeOffset ExpiresAt,
    string? IssuerName);

public record CompleteCardPaymentRequest(bool Approved);

public record AuditLogDto(
    Guid Id,
    Guid? GroupId,
    string? Actor,
    string Action,
    string Description,
    DateTimeOffset CreatedAt);

public record PlatformSettingsDto(
    decimal DefaultMaxGroupBorrowingPercent,
    int DefaultMinimumMembers,
    LendingSource DefaultLendingSources);

public record UpdatePlatformSettingsRequest(
    decimal DefaultMaxGroupBorrowingPercent,
    int DefaultMinimumMembers,
    LendingSource DefaultLendingSources);

public record GroupStatementDto(
    Guid GroupId,
    string GroupName,
    DateTimeOffset From,
    DateTimeOffset To,
    decimal OpeningBalance,
    decimal InitialDeposits,
    decimal MonthlyInstalments,
    decimal AdditionalContributions,
    decimal LoanDisbursements,
    decimal LoanRepayments,
    decimal Interest,
    decimal Penalties,
    decimal Expenses,
    decimal ClosingBalance);

public record MemberStatementDto(
    Guid MemberId,
    string MemberName,
    decimal Contributions,
    decimal Loans,
    decimal Repayments,
    decimal OutstandingBalance);

public record LoanReportRowDto(
    string LoanNumber,
    string Borrower,
    string Kind,
    decimal Principal,
    decimal Interest,
    decimal Total,
    decimal Paid,
    decimal Outstanding,
    LoanStatus Status);
