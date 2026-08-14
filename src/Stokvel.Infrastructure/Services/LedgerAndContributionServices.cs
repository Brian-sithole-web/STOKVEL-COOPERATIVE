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

public sealed class LedgerService : AppServiceBase, ILedgerService
{
    public LedgerService(StokvelDbContext db, ICurrentUser current, UserManager<ApplicationUser> users)
        : base(db, current, users) { }

    public Task<decimal> GetConfirmedBalanceAsync(Guid accountId, CancellationToken ct = default) =>
        ConfirmedBalanceAsync(accountId, ct);

    public Task<decimal> GetGroupSavingsAsync(Guid groupId, CancellationToken ct = default) =>
        GroupSavingsAsync(groupId, ct);

    public async Task<decimal> GetMemberContributionsAsync(Guid memberId, CancellationToken ct = default)
    {
        var account = await Db.FinancialAccounts.FirstOrDefaultAsync(a => a.MemberId == memberId && a.Type == AccountType.MemberContribution, ct);
        return account is null ? 0 : await ConfirmedBalanceAsync(account.Id, ct);
    }

    public async Task<LedgerSummaryDto> GetLedgerAsync(Guid groupId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        await EnsureGroupAccessAsync(groupId, ct);
        var start = from ?? DateTimeOffset.MinValue;
        var end = to ?? DateTimeOffset.UtcNow;

        var all = await Db.FinancialTransactions
            .Where(t => t.GroupId == groupId && t.Status == TransactionStatus.Confirmed)
            .OrderBy(t => t.TransactionDate)
            .ToListAsync(ct);

        var opening = Money.Round(all.Where(t => t.TransactionDate < start)
            .Sum(t => t.Direction == TransactionDirection.Inflow ? t.Amount : -t.Amount));
        var window = all.Where(t => t.TransactionDate >= start && t.TransactionDate <= end).ToList();
        var received = Money.Round(window.Where(t => t.Direction == TransactionDirection.Inflow).Sum(t => t.Amount));
        var paid = Money.Round(window.Where(t => t.Direction == TransactionDirection.Outflow).Sum(t => t.Amount));
        var closing = Money.Round(opening + received - paid);

        return new LedgerSummaryDto(opening, received, paid, closing, window.Select(MapTx).ToList());
    }

    public async Task<IReadOnlyList<TransactionDto>> ListTransactionsAsync(Guid groupId, Guid? memberId, CancellationToken ct = default)
    {
        await EnsureGroupAccessAsync(groupId, ct);
        var query = Db.FinancialTransactions.Where(t => t.GroupId == groupId);
        if (memberId.HasValue)
            query = query.Where(t => t.MemberId == memberId);
        var rows = await query.OrderByDescending(t => t.TransactionDate).Take(500).ToListAsync(ct);
        return rows.Select(MapTx).ToList();
    }

    public async Task ReverseAsync(Guid groupId, Guid transactionId, string reason, CancellationToken ct = default)
    {
        await RequireGroupRoleAsync(groupId, ct, GroupRole.StokvelAdministrator, GroupRole.Treasurer);
        var original = await Db.FinancialTransactions.FirstOrDefaultAsync(t => t.Id == transactionId && t.GroupId == groupId, ct)
                       ?? throw new NotFoundException("Transaction not found.");
        if (original.Status == TransactionStatus.Reversed)
            throw new BusinessRuleException("ALREADY_REVERSED", "This transaction has already been reversed.");

        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        original.Status = TransactionStatus.Reversed;
        var reversal = await PostAsync(
            groupId, original.AccountId, original.MemberId, TransactionType.Reversal,
            original.Direction == TransactionDirection.Inflow ? TransactionDirection.Outflow : TransactionDirection.Inflow,
            original.Amount, TransactionStatus.Confirmed, original.PaymentReference, original.PaymentMethod,
            $"Reversal of {original.TransactionNumber}: {reason}", original.RelatedLoanId, original.RelatedContributionId, ct);
        reversal.ReversesTransactionId = original.Id;
        await AuditAsync("TX_REVERSED", $"Transaction {original.TransactionNumber} was reversed. {reason}", groupId, nameof(FinancialTransaction), original.Id, ct);
        await Db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    internal static TransactionDto MapTx(FinancialTransaction t) => new(
        t.Id, t.TransactionNumber, t.GroupId, t.MemberId, t.Type, t.Direction, t.Amount,
        t.TransactionDate, t.Status, t.PaymentReference, t.PaymentMethod, t.Description, t.RecordedByUserId);
}

public sealed class ContributionService : AppServiceBase, IContributionService
{
    public ContributionService(StokvelDbContext db, ICurrentUser current, UserManager<ApplicationUser> users)
        : base(db, current, users) { }

    public async Task GenerateMonthlyObligationsAsync(Guid groupId, int year, int month, CancellationToken ct = default)
    {
        await RequireGroupRoleAsync(groupId, ct, GroupRole.StokvelAdministrator, GroupRole.Treasurer, GroupRole.Secretary);
        var group = await GetGroupAsync(groupId, ct);
        EnsureNotClosed(group);
        var rule = group.Rule ?? throw new BusinessRuleException("RULE_MISSING", "Monthly instalment rule is required.");
        if (rule.MonthlyInstalmentAmount <= 0)
            throw new BusinessRuleException("INSTALMENT_REQUIRED", "A monthly instalment amount must be configured.");

        var due = new DateTime(year, month, Math.Min(rule.InstalmentDueDay, DateTime.DaysInMonth(year, month)));
        if (!await Db.ContributionSchedules.AnyAsync(s => s.GroupId == groupId && s.Year == year && s.Month == month && s.IsGroupLevel, ct))
        {
            Db.ContributionSchedules.Add(new ContributionSchedule
            {
                GroupId = groupId,
                Year = year,
                Month = month,
                AmountDue = rule.MonthlyInstalmentAmount,
                DueDate = due,
                IsGroupLevel = true
            });
        }

        var members = await Db.GroupMembers.Where(m => m.GroupId == groupId && m.Status == MembershipStatus.Active).ToListAsync(ct);
        foreach (var member in members)
        {
            if (await Db.ContributionSchedules.AnyAsync(s => s.MemberId == member.Id && s.Year == year && s.Month == month && !s.IsGroupLevel, ct))
                continue;
            Db.ContributionSchedules.Add(new ContributionSchedule
            {
                GroupId = groupId,
                MemberId = member.Id,
                Year = year,
                Month = month,
                AmountDue = rule.MonthlyInstalmentAmount,
                DueDate = due,
                IsGroupLevel = false
            });
            Notify(member.UserId, "Monthly instalment due",
                $"{group.Name}: R{rule.MonthlyInstalmentAmount:N2} is due on {due:dd MMM yyyy}.",
                NotificationType.InstalmentDue, groupId);
        }

        await Db.SaveChangesAsync(ct);
    }

    public async Task<ContributionDto> RecordAsync(Guid groupId, RecordContributionRequest request, CancellationToken ct = default)
    {
        var group = await GetGroupAsync(groupId, ct);
        EnsureNotClosed(group);
        var membership = await FindMembershipAsync(groupId, ct);
        var isOfficer = Current.IsPlatformAdmin || (membership is not null && membership.Role is GroupRole.StokvelAdministrator or GroupRole.Treasurer);
        if (!isOfficer)
        {
            if (membership is null) throw new ForbiddenException("You cannot record a contribution for this Stokvel.");
            request = request with { MemberId = membership.Id };
        }

        var rule = group.Rule!;
        if (request.Kind == ContributionKind.AdditionalDeposit && !rule.AllowAdditionalPayments)
            throw new BusinessRuleException("ADDITIONAL_DISABLED", "Additional payments are not allowed for this Stokvel.");

        ContributionSchedule? schedule = null;
        if (request.ScheduleId.HasValue)
        {
            schedule = await Db.ContributionSchedules.FirstOrDefaultAsync(s => s.Id == request.ScheduleId && s.GroupId == groupId, ct)
                       ?? throw new NotFoundException("Instalment schedule not found.");
            if (!rule.AllowPartialPayments && request.Amount < (schedule.AmountDue - schedule.AmountPaid) && request.Kind == ContributionKind.MonthlyInstalment)
                throw new BusinessRuleException("PARTIAL_DISABLED", "Partial payments are not allowed.");
        }

        var contribution = new Contribution
        {
            GroupId = groupId,
            MemberId = request.MemberId,
            ScheduleId = request.ScheduleId,
            Amount = Money.Round(request.Amount),
            Kind = request.Kind,
            PaymentDate = request.PaymentDate,
            PaymentReference = request.PaymentReference,
            PaymentMethod = request.PaymentMethod,
            Status = TransactionStatus.Pending,
            RecordedByUserId = Current.UserId
        };
        Db.Contributions.Add(contribution);
        await AuditAsync("CONTRIBUTION_RECORDED",
            $"{(request.Kind == ContributionKind.MonthlyInstalment ? "Monthly instalment" : request.Kind.ToString())} of R{contribution.Amount:N2} recorded for {group.Name}.",
            groupId, nameof(Contribution), contribution.Id, ct);
        await Db.SaveChangesAsync(ct);
        return await MapContributionAsync(contribution, ct);
    }

    public async Task ConfirmAsync(Guid groupId, Guid contributionId, CancellationToken ct = default)
    {
        await RequireGroupRoleAsync(groupId, ct, GroupRole.StokvelAdministrator, GroupRole.Treasurer);
        var contribution = await Db.Contributions.FirstOrDefaultAsync(c => c.Id == contributionId && c.GroupId == groupId, ct)
                           ?? throw new NotFoundException("Contribution not found.");
        if (contribution.Status == TransactionStatus.Confirmed)
            return;

        var group = await GetGroupAsync(groupId, ct);
        var type = contribution.Kind switch
        {
            ContributionKind.InitialDeposit => TransactionType.InitialDeposit,
            ContributionKind.MonthlyInstalment => TransactionType.MonthlyInstalment,
            _ => TransactionType.AdditionalDeposit
        };

        await using var dbTx = await Db.Database.BeginTransactionAsync(ct);
        var capital = await GetAccountAsync(groupId, AccountType.GroupCapital, null, ct);
        var posted = await PostAsync(groupId, capital.Id, contribution.MemberId, type, TransactionDirection.Inflow,
            contribution.Amount, TransactionStatus.Confirmed, contribution.PaymentReference, contribution.PaymentMethod,
            $"{contribution.Kind} confirmed", null, contribution.Id, ct);

        if (contribution.MemberId.HasValue)
        {
            var memberAccount = await GetAccountAsync(groupId, AccountType.MemberContribution, contribution.MemberId, ct);
            await PostAsync(groupId, memberAccount.Id, contribution.MemberId, type, TransactionDirection.Inflow,
                contribution.Amount, TransactionStatus.Confirmed, contribution.PaymentReference, contribution.PaymentMethod,
                "Member contribution", null, contribution.Id, ct);
        }

        contribution.Status = TransactionStatus.Confirmed;
        contribution.FinancialTransactionId = posted.Id;

        if (contribution.ScheduleId.HasValue)
        {
            var schedule = await Db.ContributionSchedules.FirstAsync(s => s.Id == contribution.ScheduleId, ct);
            schedule.AmountPaid += contribution.Amount;
            if (schedule.AmountPaid >= schedule.AmountDue)
            {
                schedule.Status = ContributionStatus.Paid;
                schedule.PaidDate = contribution.PaymentDate.UtcDateTime.Date;
            }
            else
                schedule.Status = ContributionStatus.PartiallyPaid;
        }

        if (contribution.Kind == ContributionKind.InitialDeposit)
        {
            var confirmedInitial = await Db.Contributions
                .Where(c => c.GroupId == groupId && c.Kind == ContributionKind.InitialDeposit && c.Status == TransactionStatus.Confirmed)
                .SumAsync(c => c.Amount, ct);
            confirmedInitial += contribution.Status == TransactionStatus.Confirmed ? 0 : contribution.Amount;
            var totalInitial = await Db.Contributions
                .Where(c => c.GroupId == groupId && c.Kind == ContributionKind.InitialDeposit && (c.Status == TransactionStatus.Confirmed || c.Id == contribution.Id))
                .SumAsync(c => c.Amount, ct);
            if (totalInitial >= group.Rule!.InitialDepositAmount)
            {
                group.InitialDepositCompletedAt = DateTimeOffset.UtcNow;
                if (group.Status is GroupStatus.AwaitingInitialDeposit or GroupStatus.PendingApproval)
                {
                    var members = await Db.GroupMembers.CountAsync(m => m.GroupId == groupId && m.Status == MembershipStatus.Active, ct);
                    if (members >= BusinessConstants.MinimumMembers && group.Status == GroupStatus.AwaitingInitialDeposit)
                    {
                        group.Status = GroupStatus.Active;
                        group.ActivatedAt = DateTimeOffset.UtcNow;
                    }
                }
                await NotifyGroupOfficersAsync(groupId, "Initial deposit confirmed",
                    $"The initial deposit for {group.Name} has been confirmed.", NotificationType.InitialDepositConfirmed, ct);
            }
        }

        var payer = contribution.MemberId.HasValue
            ? await Db.GroupMembers.FirstOrDefaultAsync(m => m.Id == contribution.MemberId, ct)
            : null;
        if (payer is not null)
            Notify(payer.UserId, "Payment confirmed", $"Your payment of R{contribution.Amount:N2} to {group.Name} was confirmed.", NotificationType.General, groupId);

        await AuditAsync("CONTRIBUTION_CONFIRMED", $"R{contribution.Amount:N2} {contribution.Kind} confirmed for {group.Name}.", groupId, nameof(Contribution), contribution.Id, ct);
        await Db.SaveChangesAsync(ct);
        await dbTx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<ContributionDto>> ListAsync(Guid groupId, CancellationToken ct = default)
    {
        await EnsureGroupAccessAsync(groupId, ct);
        var rows = await Db.Contributions.Where(c => c.GroupId == groupId).OrderByDescending(c => c.PaymentDate).ToListAsync(ct);
        var list = new List<ContributionDto>();
        foreach (var c in rows)
            list.Add(await MapContributionAsync(c, ct));
        return list;
    }

    public async Task<IReadOnlyList<InstalmentDto>> ListInstalmentsAsync(Guid groupId, Guid? memberId, CancellationToken ct = default)
    {
        await EnsureGroupAccessAsync(groupId, ct);
        var group = await GetGroupAsync(groupId, ct);
        var today = DateTime.UtcNow.Date;
        var query = Db.ContributionSchedules.Where(s => s.GroupId == groupId);
        if (memberId.HasValue)
            query = query.Where(s => s.MemberId == memberId || s.IsGroupLevel);
        var rows = await query.OrderByDescending(s => s.DueDate).ToListAsync(ct);
        var members = await Db.GroupMembers.Where(m => m.GroupId == groupId).ToListAsync(ct);

        foreach (var row in rows)
        {
            if (row.Status is ContributionStatus.Paid or ContributionStatus.Waived or ContributionStatus.Cancelled)
                continue;
            var graceEnd = row.DueDate.AddDays(group.Rule?.GracePeriodDays ?? 0);
            if (row.AmountPaid >= row.AmountDue && row.AmountDue > 0)
                row.Status = ContributionStatus.Paid;
            else if (today > graceEnd)
            {
                if (row.Status != ContributionStatus.Overdue)
                {
                    row.Status = ContributionStatus.Overdue;
                    if (group.Rule is { LatePenaltyAmount: > 0 } && row.PenaltyAmount == 0)
                        row.PenaltyAmount = group.Rule.LatePenaltyAmount;
                    if (row.MemberId.HasValue)
                    {
                        var m = members.FirstOrDefault(x => x.Id == row.MemberId);
                        if (m is not null)
                            Notify(m.UserId, "Instalment overdue", $"{group.Name}: an instalment is overdue.", NotificationType.InstalmentOverdue, groupId);
                    }
                }
            }
            else if (row.AmountPaid > 0)
                row.Status = ContributionStatus.PartiallyPaid;
        }
        await Db.SaveChangesAsync(ct);

        var result = new List<InstalmentDto>();
        foreach (var s in rows)
        {
            string? name = null;
            if (s.MemberId.HasValue)
            {
                var m = members.FirstOrDefault(x => x.Id == s.MemberId);
                if (m is not null)
                {
                    var user = await Users.FindByIdAsync(m.UserId.ToString());
                    name = user?.FullName;
                }
            }
            result.Add(new InstalmentDto(s.Id, s.GroupId, s.MemberId, name, s.Year, s.Month, s.AmountDue, s.AmountPaid,
                Money.Round(s.AmountDue - s.AmountPaid), s.DueDate, s.PaidDate, s.Status, s.PenaltyAmount, s.IsGroupLevel));
        }
        return result;
    }

    private async Task<ContributionDto> MapContributionAsync(Contribution c, CancellationToken ct)
    {
        string? name = null;
        if (c.MemberId.HasValue)
        {
            var member = await Db.GroupMembers.FirstOrDefaultAsync(m => m.Id == c.MemberId, ct);
            if (member is not null)
            {
                var user = await Users.FindByIdAsync(member.UserId.ToString());
                name = user?.FullName;
            }
        }
        string? txNumber = null;
        if (c.FinancialTransactionId.HasValue)
            txNumber = await Db.FinancialTransactions.Where(t => t.Id == c.FinancialTransactionId).Select(t => t.TransactionNumber).FirstOrDefaultAsync(ct);
        return new ContributionDto(c.Id, c.GroupId, c.MemberId, name, c.Amount, c.Kind, c.PaymentDate, c.PaymentReference, c.PaymentMethod, c.Status, txNumber);
    }
}
