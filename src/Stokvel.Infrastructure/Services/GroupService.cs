using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stokvel.Application.Common;
using Stokvel.Application.Dtos;
using Stokvel.Application.Services;
using Stokvel.Domain;
using Stokvel.Domain.Entities;
using Stokvel.Infrastructure.Email;
using Stokvel.Infrastructure.Identity;
using Stokvel.Infrastructure.Persistence;

namespace Stokvel.Infrastructure.Services;

public sealed class GroupService : AppServiceBase, IGroupService
{
    private readonly IEmailSender? _emailSender;
    private readonly IConfiguration? _config;
    private readonly ILogger<GroupService>? _logger;

    public GroupService(
        StokvelDbContext db,
        ICurrentUser current,
        UserManager<ApplicationUser> users,
        IEmailSender? emailSender = null,
        IConfiguration? config = null,
        ILogger<GroupService>? logger = null) : base(db, current, users)
    {
        _emailSender = emailSender;
        _config = config;
        _logger = logger;
    }

    public async Task<GroupDetailDto> CreateAsync(CreateGroupRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BusinessRuleException("NAME_REQUIRED", "Stokvel name is required.");
        if (request.MonthlyInstalmentAmount <= 0)
            throw new BusinessRuleException("INSTALMENT_REQUIRED", "A monthly instalment amount is required.");
        if (request.InitialDepositAmount <= 0)
            throw new BusinessRuleException("DEPOSIT_REQUIRED", "An initial deposit amount is required.");
        if (request.InstalmentDueDay is < 1 or > 28)
            throw new BusinessRuleException("DUE_DAY", "Instalment due day must be between 1 and 28.");

        var group = new StokvelGroup
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Code = await NextNumberAsync("STK", ct),
            Status = GroupStatus.AwaitingMembers,
            CreatedByUserId = Current.UserId
        };

        group.Rule = new GroupRule
        {
            InitialDepositAmount = Money.Round(request.InitialDepositAmount),
            MonthlyInstalmentAmount = Money.Round(request.MonthlyInstalmentAmount),
            InstalmentDueDay = request.InstalmentDueDay,
            GracePeriodDays = request.GracePeriodDays,
            LatePenaltyAmount = Money.Round(request.LatePenaltyAmount),
            AllowPartialPayments = request.AllowPartialPayments,
            AllowAdditionalPayments = request.AllowAdditionalPayments,
            MemberLoansEnabled = request.MemberLoansEnabled,
            MaxGroupBorrowingPercentOfSavings = request.MaxGroupBorrowingPercentOfSavings,
            MemberLoanMultiplier = request.MemberLoanMultiplier,
            AllowedLendingSources = request.AllowedLendingSources,
            ApprovalWorkflow = request.ApprovalWorkflow,
            DefaultInterestRatePercent = request.DefaultInterestRatePercent,
            MinimumMembers = BusinessConstants.MinimumMembers
        };

        Db.StokvelGroups.Add(group);

        var membership = new GroupMember
        {
            GroupId = group.Id,
            UserId = Current.UserId,
            Role = GroupRole.StokvelAdministrator,
            Status = MembershipStatus.Active
        };
        Db.GroupMembers.Add(membership);

        Db.FinancialAccounts.AddRange(
            new FinancialAccount { GroupId = group.Id, Type = AccountType.GroupCapital, Name = $"{group.Name} — Group capital" },
            new FinancialAccount { GroupId = group.Id, Type = AccountType.LoanReceivable, Name = $"{group.Name} — Loans receivable" },
            new FinancialAccount { GroupId = group.Id, Type = AccountType.CooperativePool, Name = $"{group.Name} — Cooperative pool" },
            new FinancialAccount { GroupId = group.Id, MemberId = membership.Id, Type = AccountType.MemberContribution, Name = "Member contributions" }
        );

        await AuditAsync("GROUP_CREATED", $"{group.Name} was created.", group.Id, nameof(StokvelGroup), group.Id, ct);
        await Db.SaveChangesAsync(ct);
        return await GetAsync(group.Id, ct);
    }

    public async Task<IReadOnlyList<GroupListItemDto>> ListAsync(CancellationToken ct = default)
    {
        IQueryable<StokvelGroup> query = Db.StokvelGroups.Include(g => g.Rule).Include(g => g.Members);
        if (!Current.IsPlatformAdmin)
        {
            var ids = await Db.GroupMembers.Where(m => m.UserId == Current.UserId && m.Status == MembershipStatus.Active)
                .Select(m => m.GroupId).ToListAsync(ct);
            query = query.Where(g => ids.Contains(g.Id));
        }

        var groups = await query.OrderByDescending(g => g.CreatedAt).ToListAsync(ct);
        var result = new List<GroupListItemDto>();
        foreach (var g in groups)
        {
            var adminId = g.Members.FirstOrDefault(m => m.Role == GroupRole.StokvelAdministrator)?.UserId ?? g.CreatedByUserId;
            var admin = await Users.FindByIdAsync(adminId.ToString());
            var savings = await GroupSavingsAsync(g.Id, ct);
            var outstanding = await OutstandingLoansAsync(g.Id, ct);
            var loanStatuses = await Db.GroupLoans.Where(l => l.GroupId == g.Id).Select(l => l.Status).ToListAsync(ct);
            result.Add(new GroupListItemDto(
                g.Id, g.Name, g.Code, g.Status, admin?.FullName ?? "—",
                g.Members.Count(m => m.Status == MembershipStatus.Active),
                g.Rule?.InitialDepositAmount ?? 0,
                g.Rule?.MonthlyInstalmentAmount ?? 0,
                savings, outstanding, DisplayLoanStatus(loanStatuses), g.CreatedAt));
        }
        return result;
    }

    public async Task<GroupDetailDto> GetAsync(Guid groupId, CancellationToken ct = default)
    {
        await EnsureGroupAccessAsync(groupId, ct);
        var g = await GetGroupAsync(groupId, ct);
        var memberCount = await Db.GroupMembers.CountAsync(m => m.GroupId == groupId && m.Status == MembershipStatus.Active, ct);
        var adminId = await Db.GroupMembers.Where(m => m.GroupId == groupId && m.Role == GroupRole.StokvelAdministrator)
            .Select(m => m.UserId).FirstOrDefaultAsync(ct);
        var admin = await Users.FindByIdAsync((adminId == Guid.Empty ? g.CreatedByUserId : adminId).ToString());
        var financials = await BuildFinancialsAsync(groupId, ct);
        var rule = g.Rule ?? throw new BusinessRuleException("RULE_MISSING", "Group rules are not configured.");
        return new GroupDetailDto(
            g.Id, g.Name, g.Description, g.Code, g.Status, g.CreatedAt, admin?.FullName ?? "—", memberCount,
            MapRule(rule), financials);
    }

    public async Task UpdateRulesAsync(Guid groupId, UpdateGroupRulesRequest request, CancellationToken ct = default)
    {
        await RequireGroupRoleAsync(groupId, ct, GroupRole.StokvelAdministrator);
        var group = await GetGroupAsync(groupId, ct);
        EnsureNotClosed(group);
        var rule = group.Rule ?? throw new BusinessRuleException("RULE_MISSING", "Group rules are not configured.");
        rule.InitialDepositAmount = Money.Round(request.InitialDepositAmount);
        rule.MonthlyInstalmentAmount = Money.Round(request.MonthlyInstalmentAmount);
        rule.InstalmentDueDay = request.InstalmentDueDay;
        rule.GracePeriodDays = request.GracePeriodDays;
        rule.LatePenaltyAmount = Money.Round(request.LatePenaltyAmount);
        rule.AllowPartialPayments = request.AllowPartialPayments;
        rule.AllowAdditionalPayments = request.AllowAdditionalPayments;
        rule.MemberLoansEnabled = request.MemberLoansEnabled;
        rule.MaxGroupBorrowingPercentOfSavings = request.MaxGroupBorrowingPercentOfSavings;
        rule.MemberLoanMultiplier = request.MemberLoanMultiplier;
        rule.MinimumGroupAgeDays = request.MinimumGroupAgeDays;
        rule.MinimumMonthlyInstalments = request.MinimumMonthlyInstalments;
        rule.MinimumSavingsBalance = request.MinimumSavingsBalance;
        rule.RequireNoOverdueInstalments = request.RequireNoOverdueInstalments;
        rule.RequireNoDefaultedLoan = request.RequireNoDefaultedLoan;
        rule.AllowedLendingSources = request.AllowedLendingSources;
        rule.ApprovalWorkflow = request.ApprovalWorkflow;
        rule.DefaultInterestRatePercent = request.DefaultInterestRatePercent;
        await AuditAsync("RULES_UPDATED", $"{group.Name} rules were updated.", groupId, nameof(GroupRule), rule.Id, ct);
        await Db.SaveChangesAsync(ct);
    }

    public async Task<InviteResultDto> InviteAsync(Guid groupId, InviteMemberRequest request, CancellationToken ct = default)
    {
        await RequireGroupRoleAsync(groupId, ct, GroupRole.StokvelAdministrator, GroupRole.Secretary);
        var group = await GetGroupAsync(groupId, ct);
        EnsureNotClosed(group);
        var email = request.Email.Trim().ToLowerInvariant();
        var invite = new GroupInvitation
        {
            GroupId = groupId,
            Email = email,
            Token = Convert.ToHexString(Guid.NewGuid().ToByteArray()) + Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddDays(14),
            InvitedByUserId = Current.UserId
        };
        Db.GroupInvitations.Add(invite);

        var existing = await Users.FindByEmailAsync(email);
        if (existing is not null)
            Notify(existing.Id, "Stokvel invitation", $"You have been invited to join {group.Name}.", NotificationType.Invitation, groupId);

        await AuditAsync("MEMBER_INVITED", $"{email} was invited to {group.Name}.", groupId, nameof(GroupInvitation), invite.Id, ct);
        await Db.SaveChangesAsync(ct);

        var inviteUrl = $"{MailLink.ResolveAppBaseUrl(_config, request.ClientOrigin)}/invite?token={invite.Token}";
        var emailSent = false;
        var message = $"Invitation created for {email}.";
        try
        {
            if (_emailSender is null)
                throw new InvalidOperationException("Email sender is not available.");
            await _emailSender.SendAsync(
                email,
                $"You are invited to join {group.Name} on pkvela",
                InvitationEmail.BuildHtml(group.Name, inviteUrl),
                ct);
            emailSent = true;
            message = $"An email was sent to {email} with a link to create a password and join {group.Name}.";
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Could not email invitation to {Email}", email);
            message = $"Invitation was created, but the email to {email} could not be sent. Share this link instead: {inviteUrl}";
        }

        return new InviteResultDto(email, invite.Token, invite.ExpiresAt, emailSent, message);
    }

    public async Task<AuthResponse> AcceptInviteAsync(AcceptInviteRequest request, CancellationToken ct = default)
    {
        throw new BusinessRuleException("USE_AUTH", "Accept invite is handled by Auth+Group combined endpoint.");
    }

    public async Task AddMemberByEmailAsync(Guid groupId, string email, GroupRole role, CancellationToken ct = default)
    {
        await RequireGroupRoleAsync(groupId, ct, GroupRole.StokvelAdministrator, GroupRole.Secretary);
        var group = await GetGroupAsync(groupId, ct);
        EnsureNotClosed(group);
        var user = await Users.FindByEmailAsync(email.Trim())
                   ?? throw new NotFoundException("No registered user exists with that email. Send an invitation instead.");
        if (await Db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == user.Id, ct))
            throw new BusinessRuleException("ALREADY_MEMBER", "This person is already a member of the Stokvel.");

        var member = new GroupMember { GroupId = groupId, UserId = user.Id, Role = role, Status = MembershipStatus.Active };
        Db.GroupMembers.Add(member);
        Db.FinancialAccounts.Add(new FinancialAccount
        {
            GroupId = groupId,
            MemberId = member.Id,
            Type = AccountType.MemberContribution,
            Name = $"{user.FullName} contributions"
        });

        Notify(user.Id, "Joined Stokvel", $"You are now a member of {group.Name}.", NotificationType.Invitation, groupId);
        await MaybeAdvanceMembershipStatusAsync(group, ct);
        await AuditAsync("MEMBER_ADDED", $"{user.FullName} joined {group.Name}.", groupId, nameof(GroupMember), member.Id, ct);
        await Db.SaveChangesAsync(ct);
    }

    public async Task SubmitForApprovalAsync(Guid groupId, CancellationToken ct = default)
    {
        await RequireGroupRoleAsync(groupId, ct, GroupRole.StokvelAdministrator);
        var group = await GetGroupAsync(groupId, ct);
        var count = await Db.GroupMembers.CountAsync(m => m.GroupId == groupId && m.Status == MembershipStatus.Active, ct);
        if (count < BusinessConstants.MinimumMembers)
            throw new BusinessRuleException("MIN_MEMBERS", $"A Stokvel requires at least {BusinessConstants.MinimumMembers} members before activation.");
        if (group.Rule is null || group.Rule.MonthlyInstalmentAmount <= 0)
            throw new BusinessRuleException("INSTALMENT_REQUIRED", "A monthly instalment rule is required.");
        group.Status = GroupStatus.PendingApproval;
        await NotifyPlatformAdminsAsync("Stokvel ready for approval", $"{group.Name} has {count} members and is awaiting activation.", NotificationType.MembersReached, groupId, ct);
        await AuditAsync("GROUP_SUBMITTED", $"{group.Name} was submitted for platform approval.", groupId, nameof(StokvelGroup), groupId, ct);
        await Db.SaveChangesAsync(ct);
    }

    public async Task ApproveAndActivateAsync(Guid groupId, CancellationToken ct = default)
    {
        if (!Current.IsPlatformAdmin)
            throw new ForbiddenException("Only the Platform Administrator can approve a Stokvel.");
        var group = await GetGroupAsync(groupId, ct);
        var count = await Db.GroupMembers.CountAsync(m => m.GroupId == groupId && m.Status == MembershipStatus.Active, ct);
        if (count < BusinessConstants.MinimumMembers)
            throw new BusinessRuleException("MIN_MEMBERS", $"A Stokvel requires at least {BusinessConstants.MinimumMembers} members.");

        var depositDone = group.InitialDepositCompletedAt.HasValue;
        if (depositDone)
        {
            group.Status = GroupStatus.Active;
            group.ActivatedAt = DateTime.UtcNow;
            await NotifyGroupOfficersAsync(groupId, "Stokvel activated", $"{group.Name} is now an active Stokvel.", NotificationType.GroupActivated, ct);
        }
        else
        {
            group.Status = GroupStatus.AwaitingInitialDeposit;
            await NotifyGroupOfficersAsync(groupId, "Initial deposit due", $"{group.Name} has been approved. Record the initial group deposit of R{group.Rule!.InitialDepositAmount:N2} to become financially active.", NotificationType.InitialDepositDue, ct);
        }

        await AuditAsync("GROUP_APPROVED", $"AD approved {group.Name}.", groupId, nameof(StokvelGroup), groupId, ct);
        await Db.SaveChangesAsync(ct);
    }

    public async Task SuspendAsync(Guid groupId, string reason, CancellationToken ct = default)
    {
        if (!Current.IsPlatformAdmin)
            throw new ForbiddenException("Only the Platform Administrator can suspend a Stokvel.");
        var group = await GetGroupAsync(groupId, ct);
        group.Status = GroupStatus.Suspended;
        group.SuspensionReason = reason;
        await AuditAsync("GROUP_SUSPENDED", $"{group.Name} was suspended. {reason}", groupId, nameof(StokvelGroup), groupId, ct);
        await Db.SaveChangesAsync(ct);
    }

    public async Task CloseAsync(Guid groupId, CancellationToken ct = default)
    {
        if (!Current.IsPlatformAdmin)
            throw new ForbiddenException("Only the Platform Administrator can close a Stokvel.");
        var group = await GetGroupAsync(groupId, ct);
        group.Status = GroupStatus.Closed;
        group.ClosedAt = DateTime.UtcNow;
        await AuditAsync("GROUP_CLOSED", $"{group.Name} was closed.", groupId, nameof(StokvelGroup), groupId, ct);
        await Db.SaveChangesAsync(ct);
    }

    public async Task DeleteInactiveAsync(Guid groupId, CancellationToken ct = default)
    {
        if (!Current.IsPlatformAdmin)
            throw new ForbiddenException("Only the Platform Administrator can delete a Stokvel.");

        var group = await Db.StokvelGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == groupId, ct)
                    ?? throw new NotFoundException("Stokvel group not found.");

        if (group.Status == GroupStatus.Active)
            throw new BusinessRuleException("GROUP_ACTIVE", "An active Stokvel cannot be deleted. Suspend or close it first.");

        var outstanding = await OutstandingLoansAsync(groupId, ct);
        if (outstanding > 0)
            throw new BusinessRuleException("OUTSTANDING_LOANS", "This Stokvel still has outstanding loans and cannot be deleted.");

        await using var tx = await Db.Database.BeginTransactionAsync(ct);

        var memberLoanIds = Db.MemberLoans.Where(l => l.GroupId == groupId).Select(l => l.Id);
        await Db.MemberLoanApprovals.Where(a => memberLoanIds.Contains(a.MemberLoanId)).ExecuteDeleteAsync(ct);
        await Db.MemberLoanRepayments.Where(r => memberLoanIds.Contains(r.MemberLoanId)).ExecuteDeleteAsync(ct);
        await Db.MemberLoans.Where(l => l.GroupId == groupId).ExecuteDeleteAsync(ct);

        var groupLoanIds = Db.GroupLoans.Where(l => l.GroupId == groupId).Select(l => l.Id);
        await Db.GroupLoanApprovals.Where(a => groupLoanIds.Contains(a.GroupLoanId)).ExecuteDeleteAsync(ct);
        await Db.GroupLoanRepayments.Where(r => groupLoanIds.Contains(r.GroupLoanId)).ExecuteDeleteAsync(ct);
        await Db.GroupLoans.Where(l => l.GroupId == groupId).ExecuteDeleteAsync(ct);

        await Db.Contributions.Where(c => c.GroupId == groupId).ExecuteDeleteAsync(ct);
        await Db.ContributionSchedules.Where(s => s.GroupId == groupId).ExecuteDeleteAsync(ct);
        await Db.FinancialTransactions
            .Where(t => t.GroupId == groupId && t.ReversesTransactionId != null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ReversesTransactionId, (Guid?)null), ct);
        await Db.FinancialTransactions.Where(t => t.GroupId == groupId).ExecuteDeleteAsync(ct);
        await Db.Documents.Where(d => d.GroupId == groupId).ExecuteDeleteAsync(ct);
        await Db.FinancialAccounts.Where(a => a.GroupId == groupId).ExecuteDeleteAsync(ct);
        await Db.GroupInvitations.Where(i => i.GroupId == groupId).ExecuteDeleteAsync(ct);
        await Db.GroupRules.Where(r => r.GroupId == groupId).ExecuteDeleteAsync(ct);
        await Db.GroupMembers.Where(m => m.GroupId == groupId).ExecuteDeleteAsync(ct);
        await Db.Announcements.Where(a => a.GroupId == groupId).ExecuteDeleteAsync(ct);
        await Db.Notifications.Where(n => n.GroupId == groupId).ExecuteDeleteAsync(ct);
        await Db.StokvelGroups.Where(g => g.Id == groupId).ExecuteDeleteAsync(ct);

        await AuditAsync("GROUP_DELETED", $"{group.Name} ({group.Code}) was deleted.", null, nameof(StokvelGroup), groupId, ct);
        await Db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<MemberDto>> GetMembersAsync(Guid groupId, CancellationToken ct = default)
    {
        await EnsureGroupAccessAsync(groupId, ct);
        var members = await Db.GroupMembers.Where(m => m.GroupId == groupId).OrderBy(m => m.JoinedAt).ToListAsync(ct);
        var list = new List<MemberDto>();
        foreach (var m in members)
        {
            var user = await Users.FindByIdAsync(m.UserId.ToString());
            var contrib = 0m;
            var account = await Db.FinancialAccounts.FirstOrDefaultAsync(a => a.MemberId == m.Id && a.Type == AccountType.MemberContribution, ct);
            if (account is not null) contrib = await ConfirmedBalanceAsync(account.Id, ct);
            var outstandingInst = await Db.ContributionSchedules
                .Where(s => s.MemberId == m.Id && s.Status != ContributionStatus.Paid && s.Status != ContributionStatus.Waived && s.Status != ContributionStatus.Cancelled)
                .SumAsync(s => s.AmountDue - s.AmountPaid, ct);
            var loanBal = await MemberLoanOutstandingAsync(m.Id, ct);
            list.Add(new MemberDto(m.Id, m.UserId, user?.FullName ?? "—", user?.Email ?? "—", m.Role, m.Status, m.JoinedAt, contrib, outstandingInst, loanBal));
        }
        return list;
    }

    public async Task JoinWithInviteTokenAsync(GroupInvitation invite, ApplicationUser user, CancellationToken ct)
    {
        var group = await GetGroupAsync(invite.GroupId, ct);
        if (await Db.GroupMembers.AnyAsync(m => m.GroupId == invite.GroupId && m.UserId == user.Id, ct))
        {
            invite.Status = InvitationStatus.Accepted;
            await Db.SaveChangesAsync(ct);
            return;
        }

        var member = new GroupMember { GroupId = invite.GroupId, UserId = user.Id, Role = GroupRole.Member, Status = MembershipStatus.Active };
        Db.GroupMembers.Add(member);
        Db.FinancialAccounts.Add(new FinancialAccount
        {
            GroupId = invite.GroupId,
            MemberId = member.Id,
            Type = AccountType.MemberContribution,
            Name = $"{user.FullName} contributions"
        });
        invite.Status = InvitationStatus.Accepted;
        await MaybeAdvanceMembershipStatusAsync(group, ct);
        await AuditAsync("INVITE_ACCEPTED", $"{user.FullName} joined {group.Name}.", group.Id, nameof(GroupMember), member.Id, ct);
        await Db.SaveChangesAsync(ct);
    }

    private async Task MaybeAdvanceMembershipStatusAsync(StokvelGroup group, CancellationToken ct)
    {
        var count = await Db.GroupMembers.CountAsync(m => m.GroupId == group.Id && m.Status == MembershipStatus.Active, ct);
        if (count >= BusinessConstants.MinimumMembers && group.Status == GroupStatus.AwaitingMembers)
        {
            group.Status = GroupStatus.PendingApproval;
            await NotifyGroupOfficersAsync(group.Id, "Minimum members reached", $"{group.Name} now has {count} members and is ready for activation.", NotificationType.MembersReached, ct);
            await NotifyPlatformAdminsAsync("Stokvel ready", $"{group.Name} reached {count} members.", NotificationType.MembersReached, group.Id, ct);
        }
    }

    private async Task<decimal> OutstandingLoansAsync(Guid groupId, CancellationToken ct)
    {
        var group = await Db.GroupLoans.Where(l => l.GroupId == groupId && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Disbursed || l.Status == LoanStatus.PartiallyRepaid))
            .ToListAsync(ct);
        var member = await Db.MemberLoans.Where(l => l.GroupId == groupId && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Disbursed || l.Status == LoanStatus.PartiallyRepaid))
            .ToListAsync(ct);
        decimal sum = 0;
        foreach (var l in group)
        {
            var paid = await Db.GroupLoanRepayments.Where(r => r.GroupLoanId == l.Id).SumAsync(r => r.AmountPaid, ct);
            sum += l.TotalRepayable - paid;
        }
        foreach (var l in member)
        {
            var paid = await Db.MemberLoanRepayments.Where(r => r.MemberLoanId == l.Id).SumAsync(r => r.AmountPaid, ct);
            sum += l.TotalRepayable - paid;
        }
        return Money.Round(sum);
    }

    private async Task<decimal> MemberLoanOutstandingAsync(Guid memberId, CancellationToken ct)
    {
        var loans = await Db.MemberLoans.Where(l => l.MemberId == memberId && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Disbursed || l.Status == LoanStatus.PartiallyRepaid)).ToListAsync(ct);
        decimal sum = 0;
        foreach (var l in loans)
        {
            var paid = await Db.MemberLoanRepayments.Where(r => r.MemberLoanId == l.Id).SumAsync(r => r.AmountPaid, ct);
            sum += l.TotalRepayable - paid;
        }
        return Money.Round(sum);
    }

    internal async Task<GroupFinancialSummaryDto> BuildFinancialsAsync(Guid groupId, CancellationToken ct)
    {
        var txs = await Db.FinancialTransactions.Where(t => t.GroupId == groupId && t.Status == TransactionStatus.Confirmed).ToListAsync(ct);

        var initial = txs.Where(t => t.Type == TransactionType.InitialDeposit && t.Direction == TransactionDirection.Inflow).Sum(t => t.Amount);
        var monthly = txs.Where(t => t.Type == TransactionType.MonthlyInstalment && t.Direction == TransactionDirection.Inflow).Sum(t => t.Amount);
        var additional = txs.Where(t => t.Type == TransactionType.AdditionalDeposit && t.Direction == TransactionDirection.Inflow).Sum(t => t.Amount);
        var repayments = txs.Where(t => t.Type == TransactionType.LoanRepayment && t.Direction == TransactionDirection.Inflow).Sum(t => t.Amount);
        var interest = txs.Where(t => t.Type == TransactionType.InterestReceived).Sum(t => t.Amount);
        var penalties = txs.Where(t => t.Type == TransactionType.PenaltyReceived).Sum(t => t.Amount);
        var disbursed = txs.Where(t => t.Type is TransactionType.GroupLoanDisbursement or TransactionType.MemberLoanDisbursement && t.Direction == TransactionDirection.Outflow).Sum(t => t.Amount);
        var expenses = txs.Where(t => t.Type is TransactionType.ApprovedExpense or TransactionType.GroupWithdrawal or TransactionType.Refund or TransactionType.OtherExpense && t.Direction == TransactionDirection.Outflow).Sum(t => t.Amount);
        var savings = await GroupSavingsAsync(groupId, ct);
        var outstanding = await OutstandingLoansAsync(groupId, ct);
        return new GroupFinancialSummaryDto(
            Money.Round(initial), Money.Round(monthly), Money.Round(additional), savings, savings,
            Money.Round(disbursed), outstanding, Money.Round(repayments), Money.Round(interest), Money.Round(penalties), Money.Round(expenses));
    }

    internal static GroupRuleDto MapRule(GroupRule r) => new(
        r.InitialDepositAmount, r.MonthlyInstalmentAmount, r.InstalmentDueDay, r.GracePeriodDays, r.LatePenaltyAmount,
        r.AllowPartialPayments, r.AllowAdditionalPayments, r.MemberLoansEnabled, r.MaxGroupBorrowingPercentOfSavings,
        r.MemberLoanMultiplier, r.MinimumMembers, r.MinimumGroupAgeDays, r.MinimumMonthlyInstalments, r.MinimumSavingsBalance,
        r.RequireNoOverdueInstalments, r.RequireNoDefaultedLoan, r.AllowedLendingSources, r.ApprovalWorkflow, r.DefaultInterestRatePercent);
}
