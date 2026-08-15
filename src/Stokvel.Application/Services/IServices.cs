using Stokvel.Application.Common;
using Stokvel.Application.Dtos;
using Stokvel.Domain;

namespace Stokvel.Application.Services;

public interface ISetupService
{
    Task<SetupStatusDto> GetStatusAsync(CancellationToken ct = default);
    Task<AuthResponse> InitializeAsync(InitializeSetupRequest request, CancellationToken ct = default);
}

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> MeAsync(CancellationToken ct = default);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);
    Task<AuthResponse> AcceptInviteAsync(AcceptInviteRequest request, CancellationToken ct = default);
}

public interface IGroupService
{
    Task<GroupDetailDto> CreateAsync(CreateGroupRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<GroupListItemDto>> ListAsync(CancellationToken ct = default);
    Task<GroupDetailDto> GetAsync(Guid groupId, CancellationToken ct = default);
    Task UpdateRulesAsync(Guid groupId, UpdateGroupRulesRequest request, CancellationToken ct = default);
    Task<InviteResultDto> InviteAsync(Guid groupId, InviteMemberRequest request, CancellationToken ct = default);
    Task<AuthResponse> AcceptInviteAsync(AcceptInviteRequest request, CancellationToken ct = default);
    Task AddMemberByEmailAsync(Guid groupId, string email, GroupRole role, CancellationToken ct = default);
    Task SubmitForApprovalAsync(Guid groupId, CancellationToken ct = default);
    Task ApproveAndActivateAsync(Guid groupId, CancellationToken ct = default);
    Task SuspendAsync(Guid groupId, string reason, CancellationToken ct = default);
    Task CloseAsync(Guid groupId, CancellationToken ct = default);
    Task DeleteInactiveAsync(Guid groupId, CancellationToken ct = default);
    Task<IReadOnlyList<MemberDto>> GetMembersAsync(Guid groupId, CancellationToken ct = default);
}

public interface IContributionService
{
    Task GenerateMonthlyObligationsAsync(Guid groupId, int year, int month, CancellationToken ct = default);
    Task<ContributionDto> RecordAsync(Guid groupId, RecordContributionRequest request, CancellationToken ct = default);
    Task ConfirmAsync(Guid groupId, Guid contributionId, CancellationToken ct = default);
    Task<IReadOnlyList<ContributionDto>> ListAsync(Guid groupId, CancellationToken ct = default);
    Task<IReadOnlyList<InstalmentDto>> ListInstalmentsAsync(Guid groupId, Guid? memberId, CancellationToken ct = default);
}

public interface ILedgerService
{
    Task<decimal> GetConfirmedBalanceAsync(Guid accountId, CancellationToken ct = default);
    Task<decimal> GetGroupSavingsAsync(Guid groupId, CancellationToken ct = default);
    Task<decimal> GetMemberContributionsAsync(Guid memberId, CancellationToken ct = default);
    Task<LedgerSummaryDto> GetLedgerAsync(Guid groupId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default);
    Task<IReadOnlyList<TransactionDto>> ListTransactionsAsync(Guid groupId, Guid? memberId, CancellationToken ct = default);
    Task ReverseAsync(Guid groupId, Guid transactionId, string reason, CancellationToken ct = default);
}

public interface ILoanService
{
    Task<EligibilityResultDto> GroupEligibilityAsync(Guid groupId, decimal requestedAmount, LendingSource source, CancellationToken ct = default);
    Task<EligibilityResultDto> MemberEligibilityAsync(Guid groupId, Guid memberId, decimal requestedAmount, CancellationToken ct = default);
    Task<GroupLoanDto> CreateGroupLoanAsync(Guid groupId, CreateGroupLoanRequest request, CancellationToken ct = default);
    Task<GroupLoanDto> SubmitGroupLoanAsync(Guid groupId, Guid loanId, CancellationToken ct = default);
    Task<GroupLoanDto> DecideGroupLoanAsync(Guid groupId, Guid loanId, DecideLoanRequest request, CancellationToken ct = default);
    Task<GroupLoanDto> DisburseGroupLoanAsync(Guid groupId, Guid loanId, CancellationToken ct = default);
    Task RecordGroupRepaymentAsync(Guid groupId, Guid loanId, RecordRepaymentRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<GroupLoanDto>> ListGroupLoansAsync(Guid? groupId, CancellationToken ct = default);
    Task<GroupLoanDto> GetGroupLoanAsync(Guid loanId, CancellationToken ct = default);
    Task<IReadOnlyList<LoanRepaymentDto>> GroupScheduleAsync(Guid loanId, CancellationToken ct = default);

    Task<MemberLoanDto> CreateMemberLoanAsync(Guid groupId, CreateMemberLoanRequest request, CancellationToken ct = default);
    Task<MemberLoanDto> DecideMemberLoanAsync(Guid groupId, Guid loanId, DecideLoanRequest request, CancellationToken ct = default);
    Task<MemberLoanDto> DisburseMemberLoanAsync(Guid groupId, Guid loanId, CancellationToken ct = default);
    Task RecordMemberRepaymentAsync(Guid groupId, Guid loanId, RecordRepaymentRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<MemberLoanDto>> ListMemberLoansAsync(Guid groupId, Guid? memberId, CancellationToken ct = default);
    Task<MemberLoanDto> GetMemberLoanAsync(Guid loanId, CancellationToken ct = default);
    Task<IReadOnlyList<LoanRepaymentDto>> MemberScheduleAsync(Guid loanId, CancellationToken ct = default);
}

public interface IDashboardService
{
    Task<PlatformDashboardDto> PlatformAsync(CancellationToken ct = default);
    Task<GroupDashboardDto> GroupAsync(Guid groupId, CancellationToken ct = default);
    Task<MemberDashboardDto> MemberAsync(Guid groupId, CancellationToken ct = default);
}

public interface IReportService
{
    Task<GroupStatementDto> GroupStatementAsync(Guid groupId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task<IReadOnlyList<MemberStatementDto>> MemberStatementsAsync(Guid groupId, CancellationToken ct = default);
    Task<IReadOnlyList<LoanReportRowDto>> LoanReportAsync(Guid? groupId, CancellationToken ct = default);
}

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> MineAsync(CancellationToken ct = default);
    Task MarkReadAsync(Guid id, CancellationToken ct = default);
}

public interface IAuditService
{
    Task<IReadOnlyList<AuditLogDto>> ListAsync(Guid? groupId, CancellationToken ct = default);
}

public interface ISettingsService
{
    Task<PlatformSettingsDto> GetAsync(CancellationToken ct = default);
    Task UpdateAsync(UpdatePlatformSettingsRequest request, CancellationToken ct = default);
}
