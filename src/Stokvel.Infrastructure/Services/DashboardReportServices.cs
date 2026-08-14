using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stokvel.Application.Common;
using Stokvel.Application.Dtos;
using Stokvel.Application.Services;
using Stokvel.Domain;
using Stokvel.Domain.Entities;
using Stokvel.Infrastructure.Identity;
using Stokvel.Infrastructure.Persistence;

namespace Stokvel.Infrastructure.Services;

public sealed class DashboardService : AppServiceBase, IDashboardService
{
    public DashboardService(StokvelDbContext db, ICurrentUser current, UserManager<ApplicationUser> users)
        : base(db, current, users) { }

    public async Task<PlatformDashboardDto> PlatformAsync(CancellationToken ct = default)
    {
        if (!Current.IsPlatformAdmin)
            throw new ForbiddenException("Only the Platform Administrator can view the platform dashboard.");

        var groups = await Db.StokvelGroups.ToListAsync(ct);
        var members = await Db.GroupMembers.CountAsync(m => m.Status == MembershipStatus.Active, ct);
        var confirmed = await Db.FinancialTransactions.Where(t => t.Status == TransactionStatus.Confirmed).ToListAsync(ct);
        decimal In(TransactionType type) => confirmed.Where(t => t.Type == type && t.Direction == TransactionDirection.Inflow).Sum(t => t.Amount);

        var groupLoans = await Db.GroupLoans.ToListAsync(ct);
        var memberLoans = await Db.MemberLoans.ToListAsync(ct);
        var outstandingGroup = 0m;
        foreach (var l in groupLoans.Where(l => l.Status is LoanStatus.Active or LoanStatus.Disbursed or LoanStatus.PartiallyRepaid))
            outstandingGroup += l.TotalRepayable - await Db.GroupLoanRepayments.Where(r => r.GroupLoanId == l.Id).SumAsync(r => r.AmountPaid, ct);
        var outstandingMember = 0m;
        foreach (var l in memberLoans.Where(l => l.Status is LoanStatus.Active or LoanStatus.Disbursed or LoanStatus.PartiallyRepaid))
            outstandingMember += l.TotalRepayable - await Db.MemberLoanRepayments.Where(r => r.MemberLoanId == l.Id).SumAsync(r => r.AmountPaid, ct);

        var overdueGroup = await Db.GroupLoanRepayments.Where(r => r.Status == ContributionStatus.Overdue || (r.Status != ContributionStatus.Paid && r.DueDate < DateTime.UtcNow.Date)).SumAsync(r => r.AmountDue - r.AmountPaid, ct);
        var overdueMember = await Db.MemberLoanRepayments.Where(r => r.Status != ContributionStatus.Paid && r.DueDate < DateTime.UtcNow.Date).SumAsync(r => r.AmountDue - r.AmountPaid, ct);

        var savings = 0m;
        foreach (var g in groups)
            savings += await GroupSavingsAsync(g.Id, ct);

        return new PlatformDashboardDto(
            groups.Count,
            groups.Count(g => g.Status == GroupStatus.Active),
            groups.Count(g => g.Status is GroupStatus.PendingApproval or GroupStatus.AwaitingMembers or GroupStatus.AwaitingInitialDeposit),
            members,
            Money.Round(In(TransactionType.InitialDeposit)),
            Money.Round(In(TransactionType.MonthlyInstalment)),
            Money.Round(savings),
            Money.Round(groupLoans.Where(l => l.Status is LoanStatus.Active or LoanStatus.Disbursed or LoanStatus.PartiallyRepaid or LoanStatus.FullyRepaid).Sum(l => l.Principal)),
            Money.Round(memberLoans.Where(l => l.Status is LoanStatus.Active or LoanStatus.Disbursed or LoanStatus.PartiallyRepaid or LoanStatus.FullyRepaid).Sum(l => l.Principal)),
            Money.Round(outstandingGroup + outstandingMember),
            Money.Round(overdueGroup + overdueMember),
            Money.Round(In(TransactionType.LoanRepayment)));
    }

    public async Task<GroupDashboardDto> GroupAsync(Guid groupId, CancellationToken ct = default)
    {
        await EnsureGroupAccessAsync(groupId, ct);
        var group = await GetGroupAsync(groupId, ct);
        var financials = await new GroupService(Db, Current, Users).BuildFinancialsAsync(groupId, ct);
        var memberCount = await Db.GroupMembers.CountAsync(m => m.GroupId == groupId && m.Status == MembershipStatus.Active, ct);
        var outstandingInst = await Db.ContributionSchedules
            .Where(s => s.GroupId == groupId && s.IsGroupLevel && s.Status != ContributionStatus.Paid && s.Status != ContributionStatus.Waived && s.Status != ContributionStatus.Cancelled)
            .SumAsync(s => s.AmountDue - s.AmountPaid, ct);
        var next = await Db.ContributionSchedules.Where(s => s.GroupId == groupId && s.IsGroupLevel && s.Status != ContributionStatus.Paid)
            .OrderBy(s => s.DueDate).Select(s => (DateTime?)s.DueDate).FirstOrDefaultAsync(ct);
        return new GroupDashboardDto(
            group.Id, group.Name, group.Status, financials.CurrentSavings, financials.InitialDeposit,
            group.Rule?.MonthlyInstalmentAmount ?? 0, financials.TotalMonthlyInstalments + financials.AdditionalDeposits + financials.InitialDeposit,
            outstandingInst, financials.AvailableFunds, financials.OutstandingLoans, next, memberCount);
    }

    public async Task<MemberDashboardDto> MemberAsync(Guid groupId, CancellationToken ct = default)
    {
        var membership = await FindMembershipAsync(groupId, ct) ?? throw new ForbiddenException("You are not a member of this Stokvel.");
        var group = await GetGroupAsync(groupId, ct);
        var account = await Db.FinancialAccounts.FirstOrDefaultAsync(a => a.MemberId == membership.Id && a.Type == AccountType.MemberContribution, ct);
        var contrib = account is null ? 0 : await ConfirmedBalanceAsync(account.Id, ct);
        var outstandingInst = await Db.ContributionSchedules
            .Where(s => s.MemberId == membership.Id && s.Status != ContributionStatus.Paid && s.Status != ContributionStatus.Waived && s.Status != ContributionStatus.Cancelled)
            .SumAsync(s => s.AmountDue - s.AmountPaid, ct);
        var loans = await Db.MemberLoans.Where(l => l.MemberId == membership.Id).ToListAsync(ct);
        var active = loans.Where(l => l.Status is LoanStatus.Active or LoanStatus.Disbursed or LoanStatus.PartiallyRepaid).ToList();
        decimal outstanding = 0;
        foreach (var l in active)
            outstanding += l.TotalRepayable - await Db.MemberLoanRepayments.Where(r => r.MemberLoanId == l.Id).SumAsync(r => r.AmountPaid, ct);
        var nextPay = await Db.ContributionSchedules.Where(s => s.MemberId == membership.Id && s.Status != ContributionStatus.Paid)
            .OrderBy(s => s.DueDate).Select(s => (DateTime?)s.DueDate).FirstOrDefaultAsync(ct);
        var nextLoan = await Db.MemberLoanRepayments.Where(r => active.Select(l => l.Id).Contains(r.MemberLoanId) && r.Status != ContributionStatus.Paid)
            .OrderBy(r => r.DueDate).Select(r => (DateTime?)r.DueDate).FirstOrDefaultAsync(ct);
        var next = new[] { nextPay, nextLoan }.Where(d => d.HasValue).OrderBy(d => d).FirstOrDefault();
        return new MemberDashboardDto(group.Id, group.Name, contrib, group.Rule?.MonthlyInstalmentAmount ?? 0, outstandingInst,
            Money.Round(active.Sum(l => l.Principal)), Money.Round(outstanding), next, membership.Role);
    }
}

public sealed class ReportService : AppServiceBase, IReportService
{
    public ReportService(StokvelDbContext db, ICurrentUser current, UserManager<ApplicationUser> users)
        : base(db, current, users) { }

    public async Task<GroupStatementDto> GroupStatementAsync(Guid groupId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        await EnsureGroupAccessAsync(groupId, ct);
        var group = await GetGroupAsync(groupId, ct);
        var ledger = await new LedgerService(Db, Current, Users).GetLedgerAsync(groupId, from, to, ct);
        var txs = await Db.FinancialTransactions.Where(t => t.GroupId == groupId && t.Status == TransactionStatus.Confirmed && t.TransactionDate >= from && t.TransactionDate <= to).ToListAsync(ct);
        decimal In(TransactionType t) => Money.Round(txs.Where(x => x.Type == t && x.Direction == TransactionDirection.Inflow).Sum(x => x.Amount));
        decimal Out(params TransactionType[] types) => Money.Round(txs.Where(x => types.Contains(x.Type) && x.Direction == TransactionDirection.Outflow).Sum(x => x.Amount));
        return new GroupStatementDto(group.Id, group.Name, from, to, ledger.OpeningBalance,
            In(TransactionType.InitialDeposit), In(TransactionType.MonthlyInstalment), In(TransactionType.AdditionalDeposit),
            Out(TransactionType.GroupLoanDisbursement, TransactionType.MemberLoanDisbursement),
            In(TransactionType.LoanRepayment), In(TransactionType.InterestReceived), In(TransactionType.PenaltyReceived),
            Out(TransactionType.ApprovedExpense, TransactionType.GroupWithdrawal, TransactionType.Refund, TransactionType.OtherExpense),
            ledger.ClosingBalance);
    }

    public async Task<IReadOnlyList<MemberStatementDto>> MemberStatementsAsync(Guid groupId, CancellationToken ct = default)
    {
        await EnsureGroupAccessAsync(groupId, ct);
        var members = await Db.GroupMembers.Where(m => m.GroupId == groupId).ToListAsync(ct);
        var list = new List<MemberStatementDto>();
        foreach (var m in members)
        {
            var user = await Users.FindByIdAsync(m.UserId.ToString());
            var account = await Db.FinancialAccounts.FirstOrDefaultAsync(a => a.MemberId == m.Id && a.Type == AccountType.MemberContribution, ct);
            var contrib = account is null ? 0 : await ConfirmedBalanceAsync(account.Id, ct);
            var loans = await Db.MemberLoans.Where(l => l.MemberId == m.Id).ToListAsync(ct);
            var principal = loans.Where(l => l.Status is not (LoanStatus.Draft or LoanStatus.Rejected or LoanStatus.Cancelled)).Sum(l => l.Principal);
            decimal repaid = 0, outstanding = 0;
            foreach (var l in loans)
            {
                var paid = await Db.MemberLoanRepayments.Where(r => r.MemberLoanId == l.Id).SumAsync(r => r.AmountPaid, ct);
                repaid += paid;
                if (l.Status is LoanStatus.Active or LoanStatus.Disbursed or LoanStatus.PartiallyRepaid)
                    outstanding += l.TotalRepayable - paid;
            }
            list.Add(new MemberStatementDto(m.Id, user?.FullName ?? "—", contrib, principal, repaid, Money.Round(outstanding)));
        }
        return list;
    }

    public async Task<IReadOnlyList<LoanReportRowDto>> LoanReportAsync(Guid? groupId, CancellationToken ct = default)
    {
        var rows = new List<LoanReportRowDto>();
        var groupLoans = Db.GroupLoans.AsQueryable();
        var memberLoans = Db.MemberLoans.AsQueryable();
        if (groupId.HasValue)
        {
            await EnsureGroupAccessAsync(groupId.Value, ct);
            groupLoans = groupLoans.Where(l => l.GroupId == groupId);
            memberLoans = memberLoans.Where(l => l.GroupId == groupId);
        }
        else if (!Current.IsPlatformAdmin)
            throw new ForbiddenException("Platform report requires Platform Administrator.");

        foreach (var l in await groupLoans.ToListAsync(ct))
        {
            var g = await Db.StokvelGroups.FindAsync(new object[] { l.GroupId }, ct);
            var paid = await Db.GroupLoanRepayments.Where(r => r.GroupLoanId == l.Id).SumAsync(r => r.AmountPaid, ct);
            rows.Add(new LoanReportRowDto(l.LoanNumber, g?.Name ?? "Group", "Group", l.Principal, Money.Round(l.TotalRepayable - l.Principal), l.TotalRepayable, paid, Money.Round(l.TotalRepayable - paid), l.Status));
        }
        foreach (var l in await memberLoans.ToListAsync(ct))
        {
            var member = await Db.GroupMembers.FirstAsync(m => m.Id == l.MemberId, ct);
            var user = await Users.FindByIdAsync(member.UserId.ToString());
            var paid = await Db.MemberLoanRepayments.Where(r => r.MemberLoanId == l.Id).SumAsync(r => r.AmountPaid, ct);
            rows.Add(new LoanReportRowDto(l.LoanNumber, user?.FullName ?? "Member", "Member", l.Principal, Money.Round(l.TotalRepayable - l.Principal), l.TotalRepayable, paid, Money.Round(l.TotalRepayable - paid), l.Status));
        }
        return rows;
    }
}

public sealed class NotificationService : AppServiceBase, INotificationService
{
    public NotificationService(StokvelDbContext db, ICurrentUser current, UserManager<ApplicationUser> users)
        : base(db, current, users) { }

    public async Task<IReadOnlyList<NotificationDto>> MineAsync(CancellationToken ct = default)
    {
        return await Db.Notifications.Where(n => n.UserId == Current.UserId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(100)
            .Select(n => new NotificationDto(n.Id, n.Title, n.Message, n.Type, n.IsRead, n.CreatedAt, n.GroupId))
            .ToListAsync(ct);
    }

    public async Task MarkReadAsync(Guid id, CancellationToken ct = default)
    {
        var n = await Db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Current.UserId, ct)
                ?? throw new NotFoundException("Notification not found.");
        n.IsRead = true;
        await Db.SaveChangesAsync(ct);
    }
}

public sealed class AuditService : AppServiceBase, IAuditService
{
    public AuditService(StokvelDbContext db, ICurrentUser current, UserManager<ApplicationUser> users)
        : base(db, current, users) { }

    public async Task<IReadOnlyList<AuditLogDto>> ListAsync(Guid? groupId, CancellationToken ct = default)
    {
        if (!Current.IsPlatformAdmin)
        {
            if (!groupId.HasValue) throw new ForbiddenException("Specify a Stokvel to view its audit log.");
            await RequireGroupRoleAsync(groupId.Value, ct, GroupRole.StokvelAdministrator, GroupRole.Chairperson, GroupRole.Secretary);
        }
        else if (groupId.HasValue)
            await EnsureGroupAccessAsync(groupId.Value, ct);

        var query = Db.AuditLogs.AsQueryable();
        if (groupId.HasValue) query = query.Where(a => a.GroupId == groupId);
        var rows = await query.OrderByDescending(a => a.CreatedAt).Take(300).ToListAsync(ct);
        var list = new List<AuditLogDto>();
        foreach (var a in rows)
        {
            string? actor = null;
            if (a.ActorUserId.HasValue)
            {
                var user = await Users.FindByIdAsync(a.ActorUserId.Value.ToString());
                actor = user?.FullName;
            }
            list.Add(new AuditLogDto(a.Id, a.GroupId, actor, a.Action, a.Description, a.CreatedAt));
        }
        return list;
    }
}

public sealed class SettingsService : AppServiceBase, ISettingsService
{
    public SettingsService(StokvelDbContext db, ICurrentUser current, UserManager<ApplicationUser> users)
        : base(db, current, users) { }

    public async Task<PlatformSettingsDto> GetAsync(CancellationToken ct = default)
    {
        if (!Current.IsPlatformAdmin) throw new ForbiddenException("Only the Platform Administrator can view platform settings.");
        var s = await Db.PlatformSettings.FirstOrDefaultAsync(ct) ?? new PlatformSettings();
        return new PlatformSettingsDto(s.DefaultMaxGroupBorrowingPercent, s.DefaultMinimumMembers, s.DefaultLendingSources);
    }

    public async Task UpdateAsync(UpdatePlatformSettingsRequest request, CancellationToken ct = default)
    {
        if (!Current.IsPlatformAdmin) throw new ForbiddenException("Only the Platform Administrator can update platform settings.");
        var s = await Db.PlatformSettings.FirstOrDefaultAsync(ct);
        if (s is null)
        {
            s = new PlatformSettings { SetupCompleted = true };
            Db.PlatformSettings.Add(s);
        }
        s.DefaultMaxGroupBorrowingPercent = request.DefaultMaxGroupBorrowingPercent;
        s.DefaultMinimumMembers = request.DefaultMinimumMembers;
        s.DefaultLendingSources = request.DefaultLendingSources;
        await AuditAsync("SETTINGS_UPDATED", "Platform borrowing settings were updated.", ct: ct);
        await Db.SaveChangesAsync(ct);
    }
}
