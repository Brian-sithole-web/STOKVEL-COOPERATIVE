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

public sealed class LoanService : AppServiceBase, ILoanService
{
    public LoanService(StokvelDbContext db, ICurrentUser current, UserManager<ApplicationUser> users)
        : base(db, current, users) { }

    public async Task<EligibilityResultDto> GroupEligibilityAsync(Guid groupId, decimal requestedAmount, LendingSource source, CancellationToken ct = default)
    {
        await EnsureGroupAccessAsync(groupId, ct);
        var group = await GetGroupAsync(groupId, ct);
        var reasons = new List<string>();
        var rule = group.Rule!;

        if (group.Status == GroupStatus.Closed)
            reasons.Add("A closed Stokvel cannot create new loans.");
        if (group.Status != GroupStatus.Active)
            reasons.Add("The Stokvel must be active before borrowing.");

        var allowed = rule.AllowedLendingSources;
        if (allowed != LendingSource.Both && allowed != source)
            reasons.Add("This lending source is not enabled for the group.");

        var members = await Db.GroupMembers.CountAsync(m => m.GroupId == groupId && m.Status == MembershipStatus.Active, ct);
        var minMembers = Math.Max(rule.MinimumMembers, BusinessConstants.MinimumMembers);
        if (members < minMembers)
            reasons.Add($"This Stokvel needs at least {minMembers} members before a loan can be requested. Current members: {members}.");

        if (rule.MinimumGroupAgeDays > 0 && group.ActivatedAt.HasValue)
        {
            var age = (DateTime.UtcNow - group.ActivatedAt.Value).TotalDays;
            if (age < rule.MinimumGroupAgeDays)
                reasons.Add($"The Stokvel must be at least {rule.MinimumGroupAgeDays} days old.");
        }

        if (!group.InitialDepositCompletedAt.HasValue)
            reasons.Add("The initial deposit must be completed.");

        var paidMonths = await Db.ContributionSchedules.CountAsync(s => s.GroupId == groupId && s.IsGroupLevel && s.Status == ContributionStatus.Paid, ct);
        if (rule.MinimumMonthlyInstalments > 0 && paidMonths < rule.MinimumMonthlyInstalments)
            reasons.Add($"At least {rule.MinimumMonthlyInstalments} monthly instalments must be paid.");

        var savings = await GroupSavingsAsync(groupId, ct);
        if (savings <= 0)
            reasons.Add("The group has not raised any collective savings yet.");
        if (savings < rule.MinimumSavingsBalance)
            reasons.Add($"Minimum savings balance is R{rule.MinimumSavingsBalance:N2}.");

        var maxBorrow = Money.Round(savings);
        if (requestedAmount > maxBorrow)
            reasons.Add($"The loan cannot exceed the collective savings this Stokvel has raised (max R{maxBorrow:N2}).");

        if (rule.RequireNoOverdueInstalments)
        {
            var overdue = await Db.ContributionSchedules.AnyAsync(s => s.GroupId == groupId && s.Status == ContributionStatus.Overdue, ct);
            if (overdue) reasons.Add("There are overdue instalments.");
        }

        if (rule.RequireNoDefaultedLoan)
        {
            var def = await Db.GroupLoans.AnyAsync(l => l.GroupId == groupId && l.Status == LoanStatus.Defaulted, ct)
                      || await Db.MemberLoans.AnyAsync(l => l.GroupId == groupId && l.Status == LoanStatus.Defaulted, ct);
            if (def) reasons.Add("There is a defaulted loan.");
        }

        var existingActive = await Db.GroupLoans.AnyAsync(l => l.GroupId == groupId && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Disbursed || l.Status == LoanStatus.PartiallyRepaid), ct);
        if (existingActive)
            reasons.Add("An existing group loan is still outstanding.");

        return new EligibilityResultDto(reasons.Count == 0, maxBorrow, reasons);
    }

    public async Task<EligibilityResultDto> MemberEligibilityAsync(Guid groupId, Guid memberId, decimal requestedAmount, CancellationToken ct = default)
    {
        await EnsureGroupAccessAsync(groupId, ct);
        var group = await GetGroupAsync(groupId, ct);
        var reasons = new List<string>();
        var rule = group.Rule!;
        if (!rule.MemberLoansEnabled)
            reasons.Add("Member loans are not enabled for this Stokvel.");
        if (group.Status != GroupStatus.Active)
            reasons.Add("The Stokvel must be active.");
        if (group.Status == GroupStatus.Closed)
            reasons.Add("A closed Stokvel cannot create new loans.");

        var members = await Db.GroupMembers.CountAsync(m => m.GroupId == groupId && m.Status == MembershipStatus.Active, ct);
        var minMembers = Math.Max(rule.MinimumMembers, BusinessConstants.MinimumMembers);
        if (members < minMembers)
            reasons.Add($"This Stokvel needs at least {minMembers} members before a loan can be requested. Current members: {members}.");

        var contributions = await MemberContributionsAsync(memberId, ct);
        var savings = await GroupSavingsAsync(groupId, ct);
        var fromContributions = Money.Round(contributions * rule.MemberLoanMultiplier);
        var max = Money.Round(Math.Min(savings, fromContributions > 0 ? fromContributions : savings));
        if (savings <= 0)
            reasons.Add("The group has not raised any collective savings yet.");
        if (requestedAmount > savings)
            reasons.Add($"The loan cannot exceed the collective savings this Stokvel has raised (max R{savings:N2}).");
        else if (requestedAmount > max)
            reasons.Add($"Maximum member loan is limited to the group’s raised savings and member contributions (R{max:N2}).");

        var existing = await Db.MemberLoans.AnyAsync(l => l.MemberId == memberId && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Disbursed || l.Status == LoanStatus.PartiallyRepaid || l.Status == LoanStatus.Defaulted), ct);
        if (existing) reasons.Add("This member already has an outstanding or defaulted loan.");

        if (rule.RequireNoOverdueInstalments)
        {
            var overdue = await Db.ContributionSchedules.AnyAsync(s => s.MemberId == memberId && s.Status == ContributionStatus.Overdue, ct);
            if (overdue) reasons.Add("This member has overdue instalments.");
        }

        return new EligibilityResultDto(reasons.Count == 0, max, reasons);
    }

    public async Task<GroupLoanDto> CreateGroupLoanAsync(Guid groupId, CreateGroupLoanRequest request, CancellationToken ct = default)
    {
        await RequireGroupRoleAsync(groupId, ct, GroupRole.StokvelAdministrator, GroupRole.Chairperson, GroupRole.Treasurer);
        var group = await GetGroupAsync(groupId, ct);
        EnsureNotClosed(group);
        var eligibility = await GroupEligibilityAsync(groupId, request.Principal, request.Source, ct);
        if (!eligibility.Eligible)
            throw new BusinessRuleException("NOT_ELIGIBLE", string.Join(" ", eligibility.Reasons));
        if (request.RepaymentMonths < 1)
            throw new BusinessRuleException("TERM", "Repayment period must be at least 1 month.");

        var (total, monthly) = ComputeRepayment(request.Principal, request.InterestRatePercent, request.RepaymentMonths);
        var loan = new GroupLoan
        {
            LoanNumber = await NextNumberAsync("GL", ct),
            GroupId = groupId,
            Principal = Money.Round(request.Principal),
            InterestRatePercent = request.InterestRatePercent,
            TotalRepayable = total,
            RepaymentMonths = request.RepaymentMonths,
            MonthlyRepayment = monthly,
            Purpose = request.Purpose.Trim(),
            Status = LoanStatus.Draft,
            Source = request.Source,
            RequestedByUserId = Current.UserId
        };
        Db.GroupLoans.Add(loan);
        await AuditAsync("GROUP_LOAN_DRAFT", $"{group.Name} drafted a group loan request for R{loan.Principal:N2}.", groupId, nameof(GroupLoan), loan.Id, ct);
        await Db.SaveChangesAsync(ct);
        return await MapGroupLoanAsync(loan, ct);
    }

    public async Task<GroupLoanDto> SubmitGroupLoanAsync(Guid groupId, Guid loanId, CancellationToken ct = default)
    {
        await RequireGroupRoleAsync(groupId, ct, GroupRole.StokvelAdministrator, GroupRole.Chairperson);
        var loan = await LoadGroupLoanAsync(groupId, loanId, ct);
        if (loan.Status != LoanStatus.Draft)
            throw new BusinessRuleException("INVALID_STATE", "Only draft applications can be submitted.");
        var group = await GetGroupAsync(groupId, ct);
        loan.Status = LoanStatus.Submitted;
        foreach (var step in BuildGroupApprovalSteps(group.Rule!.ApprovalWorkflow))
            Db.GroupLoanApprovals.Add(new GroupLoanApproval { GroupLoanId = loan.Id, RequiredRole = step.Role, StepOrder = step.Order });

        await NotifyPlatformAdminsAsync("Group loan submitted", $"{group.Name} submitted a loan request for R{loan.Principal:N2}.", NotificationType.GroupLoanSubmitted, groupId, ct);
        await NotifyGroupOfficersAsync(groupId, "Group loan submitted", $"{group.Name} submitted a borrowing application for R{loan.Principal:N2}.", NotificationType.GroupLoanSubmitted, ct);
        await AuditAsync("GROUP_LOAN_SUBMITTED", $"{group.Name} submitted a loan request for R{loan.Principal:N2}.", groupId, nameof(GroupLoan), loan.Id, ct);
        await Db.SaveChangesAsync(ct);
        return await MapGroupLoanAsync(loan, ct);
    }

    public async Task<GroupLoanDto> DecideGroupLoanAsync(Guid groupId, Guid loanId, DecideLoanRequest request, CancellationToken ct = default)
    {
        var loan = await LoadGroupLoanAsync(groupId, loanId, ct);
        if (loan.RequestedByUserId == Current.UserId && !Current.IsPlatformAdmin)
            throw new BusinessRuleException("SELF_APPROVE", "A user cannot approve their own loan.");
        if (loan.Status is not (LoanStatus.Submitted or LoanStatus.UnderReview))
            throw new BusinessRuleException("INVALID_STATE", "This loan is not awaiting a decision.");

        var steps = await Db.GroupLoanApprovals.Where(a => a.GroupLoanId == loan.Id).OrderBy(a => a.StepOrder).ToListAsync(ct);
        var current = steps.FirstOrDefault(s => s.Decision == ApprovalDecision.Pending)
                      ?? throw new BusinessRuleException("NO_STEP", "No pending approval step.");
        await EnsureCanDecideAsync(groupId, current.RequiredRole, ct);

        current.ApproverUserId = Current.UserId;
        current.Decision = request.Approve ? ApprovalDecision.Approved : ApprovalDecision.Rejected;
        current.Comment = request.Comment;
        current.DecidedAt = DateTime.UtcNow;
        loan.Status = LoanStatus.UnderReview;

        var group = await GetGroupAsync(groupId, ct);
        if (!request.Approve)
        {
            loan.Status = LoanStatus.Rejected;
            loan.RejectionReason = request.Comment;
            await NotifyGroupOfficersAsync(groupId, "Group loan rejected", $"{group.Name} loan {loan.LoanNumber} was rejected.", NotificationType.GroupLoanRejected, ct);
            await AuditAsync("GROUP_LOAN_REJECTED", $"Loan {loan.LoanNumber} was rejected.", groupId, nameof(GroupLoan), loan.Id, ct);
        }
        else if (steps.All(s => s.Decision == ApprovalDecision.Approved || s.Id == current.Id))
        {
            loan.Status = LoanStatus.Approved;
            loan.ApprovedAt = DateTime.UtcNow;
            await NotifyGroupOfficersAsync(groupId, "Group loan approved", $"{group.Name} loan {loan.LoanNumber} was approved.", NotificationType.GroupLoanApproved, ct);
            await AuditAsync("GROUP_LOAN_APPROVED", $"Loan {loan.LoanNumber} was approved.", groupId, nameof(GroupLoan), loan.Id, ct);
        }

        await Db.SaveChangesAsync(ct);
        return await MapGroupLoanAsync(loan, ct);
    }

    public async Task<GroupLoanDto> DisburseGroupLoanAsync(Guid groupId, Guid loanId, CancellationToken ct = default)
    {
        await RequireGroupRoleAsync(groupId, ct, GroupRole.StokvelAdministrator, GroupRole.Treasurer);
        var loan = await LoadGroupLoanAsync(groupId, loanId, ct);
        if (loan.Status != LoanStatus.Approved)
            throw new BusinessRuleException("NOT_APPROVED", "A loan cannot be disbursed before approval.");

        var group = await GetGroupAsync(groupId, ct);
        var sourceType = loan.Source == LendingSource.CooperativeLendingPool ? AccountType.CooperativePool : AccountType.GroupCapital;
        var source = await GetAccountAsync(groupId, sourceType, null, ct);
        var receivable = await GetAccountAsync(groupId, AccountType.LoanReceivable, null, ct);
        var available = await ConfirmedBalanceAsync(source.Id, ct);
        if (loan.Principal > available)
            throw new BusinessRuleException("INSUFFICIENT_FUNDS", "Insufficient funds in the selected lending pool.");

        await using var dbTx = await Db.Database.BeginTransactionAsync(ct);
        var outflow = await PostAsync(groupId, source.Id, null, TransactionType.GroupLoanDisbursement, TransactionDirection.Outflow,
            loan.Principal, TransactionStatus.Confirmed, loan.LoanNumber, PaymentMethod.Eft, $"Disbursement {loan.LoanNumber}", loan.Id, null, ct);
        await PostAsync(groupId, receivable.Id, null, TransactionType.GroupLoanDisbursement, TransactionDirection.Inflow,
            loan.Principal, TransactionStatus.Confirmed, loan.LoanNumber, PaymentMethod.Eft, $"Receivable {loan.LoanNumber}", loan.Id, null, ct);

        loan.Status = LoanStatus.Active;
        loan.DisbursedAt = DateTime.UtcNow;
        loan.DisbursementTransactionId = outflow.Id;

        var start = DateTime.UtcNow.Date;
        foreach (var item in BuildSchedule(loan.RepaymentMonths, loan.TotalRepayable, loan.MonthlyRepayment, start,
                     (n, due, amt) => new GroupLoanRepayment { GroupLoanId = loan.Id, InstalmentNumber = n, DueDate = due, AmountDue = amt }))
            Db.GroupLoanRepayments.Add(item);

        await AuditAsync("GROUP_LOAN_DISBURSED", $"Loan {loan.LoanNumber} of R{loan.Principal:N2} was disbursed to {group.Name}.", groupId, nameof(GroupLoan), loan.Id, ct);
        await Db.SaveChangesAsync(ct);
        await dbTx.CommitAsync(ct);
        return await MapGroupLoanAsync(loan, ct);
    }

    public async Task RecordGroupRepaymentAsync(Guid groupId, Guid loanId, RecordRepaymentRequest request, CancellationToken ct = default)
    {
        await RequireGroupRoleAsync(groupId, ct, GroupRole.StokvelAdministrator, GroupRole.Treasurer);
        var loan = await LoadGroupLoanAsync(groupId, loanId, ct);
        if (loan.Status is not (LoanStatus.Active or LoanStatus.PartiallyRepaid or LoanStatus.Disbursed))
            throw new BusinessRuleException("INVALID_STATE", "This loan is not in repayment.");

        var remaining = request.Amount;
        var instalments = await Db.GroupLoanRepayments.Where(r => r.GroupLoanId == loan.Id).OrderBy(r => r.InstalmentNumber).ToListAsync(ct);
        foreach (var row in instalments)
        {
            if (remaining <= 0) break;
            var due = row.AmountDue - row.AmountPaid;
            if (due <= 0) continue;
            var pay = Math.Min(due, remaining);
            row.AmountPaid += pay;
            remaining -= pay;
            if (row.AmountPaid >= row.AmountDue)
            {
                row.Status = ContributionStatus.Paid;
                row.PaidDate = request.PaymentDate.UtcDateTime.Date;
            }
            else
                row.Status = ContributionStatus.PartiallyPaid;
        }

        var capital = await GetAccountAsync(groupId, AccountType.GroupCapital, null, ct);
        var receivable = await GetAccountAsync(groupId, AccountType.LoanReceivable, null, ct);
        await using var dbTx = await Db.Database.BeginTransactionAsync(ct);
        await PostAsync(groupId, capital.Id, null, TransactionType.LoanRepayment, TransactionDirection.Inflow,
            request.Amount, TransactionStatus.Confirmed, request.PaymentReference, request.PaymentMethod, $"Repayment {loan.LoanNumber}", loan.Id, null, ct);
        var recvBal = await ConfirmedBalanceAsync(receivable.Id, ct);
        var reduce = Math.Min(request.Amount, Math.Max(recvBal, 0));
        if (reduce > 0)
        {
            await PostAsync(groupId, receivable.Id, null, TransactionType.LoanRepayment, TransactionDirection.Outflow,
                reduce, TransactionStatus.Confirmed, request.PaymentReference, request.PaymentMethod, $"Receivable reduction {loan.LoanNumber}", loan.Id, null, ct);
        }

        var paid = instalments.Sum(i => i.AmountPaid);
        loan.Status = paid >= loan.TotalRepayable ? LoanStatus.FullyRepaid : LoanStatus.PartiallyRepaid;
        await AuditAsync("GROUP_REPAYMENT", $"Repayment of R{request.Amount:N2} recorded for loan {loan.LoanNumber}.", groupId, nameof(GroupLoan), loan.Id, ct);
        await NotifyGroupOfficersAsync(groupId, "Repayment recorded", $"R{request.Amount:N2} was recorded against {loan.LoanNumber}.", NotificationType.General, ct);
        await Db.SaveChangesAsync(ct);
        await dbTx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<GroupLoanDto>> ListGroupLoansAsync(Guid? groupId, CancellationToken ct = default)
    {
        var query = Db.GroupLoans.AsQueryable();
        if (groupId.HasValue)
        {
            await EnsureGroupAccessAsync(groupId.Value, ct);
            query = query.Where(l => l.GroupId == groupId.Value);
        }
        else if (!Current.IsPlatformAdmin)
        {
            var ids = await Db.GroupMembers.Where(m => m.UserId == Current.UserId && m.Status == MembershipStatus.Active).Select(m => m.GroupId).ToListAsync(ct);
            query = query.Where(l => ids.Contains(l.GroupId));
        }

        var loans = await query.OrderByDescending(l => l.CreatedAt).ToListAsync(ct);
        var list = new List<GroupLoanDto>();
        foreach (var l in loans)
            list.Add(await MapGroupLoanAsync(l, ct));
        return list;
    }

    public async Task<GroupLoanDto> GetGroupLoanAsync(Guid loanId, CancellationToken ct = default)
    {
        var loan = await Db.GroupLoans.FirstOrDefaultAsync(l => l.Id == loanId, ct) ?? throw new NotFoundException("Loan not found.");
        await EnsureGroupAccessAsync(loan.GroupId, ct);
        return await MapGroupLoanAsync(loan, ct);
    }

    public async Task<IReadOnlyList<LoanRepaymentDto>> GroupScheduleAsync(Guid loanId, CancellationToken ct = default)
    {
        var loan = await Db.GroupLoans.FirstOrDefaultAsync(l => l.Id == loanId, ct) ?? throw new NotFoundException("Loan not found.");
        await EnsureGroupAccessAsync(loan.GroupId, ct);
        return await Db.GroupLoanRepayments.Where(r => r.GroupLoanId == loanId).OrderBy(r => r.InstalmentNumber)
            .Select(r => new LoanRepaymentDto(r.Id, r.InstalmentNumber, r.DueDate, r.AmountDue, r.AmountPaid, r.PaidDate, r.Status))
            .ToListAsync(ct);
    }

    public async Task<MemberLoanDto> CreateMemberLoanAsync(Guid groupId, CreateMemberLoanRequest request, CancellationToken ct = default)
    {
        var membership = await RequireGroupRoleAsync(groupId, ct);
        var group = await GetGroupAsync(groupId, ct);
        EnsureNotClosed(group);
        var eligibility = await MemberEligibilityAsync(groupId, membership.Id, request.Principal, ct);
        if (!eligibility.Eligible)
            throw new BusinessRuleException("NOT_ELIGIBLE", string.Join(" ", eligibility.Reasons));

        var (total, monthly) = ComputeRepayment(request.Principal, request.InterestRatePercent, request.RepaymentMonths);
        var loan = new MemberLoan
        {
            LoanNumber = await NextNumberAsync("ML", ct),
            GroupId = groupId,
            MemberId = membership.Id,
            Principal = Money.Round(request.Principal),
            InterestRatePercent = request.InterestRatePercent,
            TotalRepayable = total,
            RepaymentMonths = request.RepaymentMonths,
            MonthlyRepayment = monthly,
            Purpose = request.Purpose.Trim(),
            Status = LoanStatus.Submitted,
            RequestedByUserId = Current.UserId
        };
        Db.MemberLoans.Add(loan);
        Db.MemberLoanApprovals.Add(new MemberLoanApproval { MemberLoanId = loan.Id, RequiredRole = "GROUP_OFFICER", StepOrder = 1 });
        var user = await Users.FindByIdAsync(Current.UserId.ToString());
        await NotifyGroupOfficersAsync(groupId, "Member loan requested", $"{user?.FullName} requested a member loan of R{loan.Principal:N2}.", NotificationType.MemberLoanSubmitted, ct);
        await AuditAsync("MEMBER_LOAN_SUBMITTED", $"{user?.FullName} requested a member loan of R{loan.Principal:N2}.", groupId, nameof(MemberLoan), loan.Id, ct);
        await Db.SaveChangesAsync(ct);
        return await MapMemberLoanAsync(loan, ct);
    }

    public async Task<MemberLoanDto> DecideMemberLoanAsync(Guid groupId, Guid loanId, DecideLoanRequest request, CancellationToken ct = default)
    {
        var actor = await RequireGroupRoleAsync(groupId, ct, GroupRole.StokvelAdministrator, GroupRole.Treasurer, GroupRole.Chairperson);
        var loan = await Db.MemberLoans.FirstOrDefaultAsync(l => l.Id == loanId && l.GroupId == groupId, ct)
                   ?? throw new NotFoundException("Member loan not found.");
        if (loan.RequestedByUserId == Current.UserId)
            throw new BusinessRuleException("SELF_APPROVE", "A user cannot approve their own loan.");
        if (loan.MemberId == actor.Id && !Current.IsPlatformAdmin)
            throw new BusinessRuleException("SELF_APPROVE", "A user cannot approve their own loan.");
        if (loan.Status is not (LoanStatus.Submitted or LoanStatus.UnderReview))
            throw new BusinessRuleException("INVALID_STATE", "This loan is not awaiting a decision.");

        var step = await Db.MemberLoanApprovals.Where(a => a.MemberLoanId == loan.Id && a.Decision == ApprovalDecision.Pending)
            .OrderBy(a => a.StepOrder).FirstOrDefaultAsync(ct) ?? throw new BusinessRuleException("NO_STEP", "No pending approval.");
        step.ApproverUserId = Current.UserId;
        step.Decision = request.Approve ? ApprovalDecision.Approved : ApprovalDecision.Rejected;
        step.Comment = request.Comment;
        step.DecidedAt = DateTime.UtcNow;

        var member = await Db.GroupMembers.FirstAsync(m => m.Id == loan.MemberId, ct);
        if (!request.Approve)
        {
            loan.Status = LoanStatus.Rejected;
            loan.RejectionReason = request.Comment;
            Notify(member.UserId, "Loan rejected", $"Member loan {loan.LoanNumber} was rejected.", NotificationType.MemberLoanRejected, groupId);
            await AuditAsync("MEMBER_LOAN_REJECTED", $"Loan {loan.LoanNumber} was rejected.", groupId, nameof(MemberLoan), loan.Id, ct);
        }
        else
        {
            loan.Status = LoanStatus.Approved;
            loan.ApprovedAt = DateTime.UtcNow;
            Notify(member.UserId, "Loan approved", $"Member loan {loan.LoanNumber} was approved.", NotificationType.MemberLoanApproved, groupId);
            await AuditAsync("MEMBER_LOAN_APPROVED", $"Loan {loan.LoanNumber} was approved.", groupId, nameof(MemberLoan), loan.Id, ct);
        }

        await Db.SaveChangesAsync(ct);
        return await MapMemberLoanAsync(loan, ct);
    }

    public async Task<MemberLoanDto> DisburseMemberLoanAsync(Guid groupId, Guid loanId, CancellationToken ct = default)
    {
        await RequireGroupRoleAsync(groupId, ct, GroupRole.StokvelAdministrator, GroupRole.Treasurer);
        var loan = await Db.MemberLoans.FirstOrDefaultAsync(l => l.Id == loanId && l.GroupId == groupId, ct)
                   ?? throw new NotFoundException("Member loan not found.");
        if (loan.Status != LoanStatus.Approved)
            throw new BusinessRuleException("NOT_APPROVED", "A loan cannot be disbursed before approval.");

        var capital = await GetAccountAsync(groupId, AccountType.GroupCapital, null, ct);
        var receivable = await GetAccountAsync(groupId, AccountType.LoanReceivable, null, ct);
        var available = await ConfirmedBalanceAsync(capital.Id, ct);
        if (loan.Principal > available)
            throw new BusinessRuleException("INSUFFICIENT_FUNDS", "Insufficient group savings to disburse this loan.");

        await using var dbTx = await Db.Database.BeginTransactionAsync(ct);
        var outflow = await PostAsync(groupId, capital.Id, loan.MemberId, TransactionType.MemberLoanDisbursement, TransactionDirection.Outflow,
            loan.Principal, TransactionStatus.Confirmed, loan.LoanNumber, PaymentMethod.Eft, $"Member disbursement {loan.LoanNumber}", loan.Id, null, ct);
        await PostAsync(groupId, receivable.Id, loan.MemberId, TransactionType.MemberLoanDisbursement, TransactionDirection.Inflow,
            loan.Principal, TransactionStatus.Confirmed, loan.LoanNumber, PaymentMethod.Eft, $"Receivable {loan.LoanNumber}", loan.Id, null, ct);
        loan.Status = LoanStatus.Active;
        loan.DisbursedAt = DateTime.UtcNow;
        loan.DisbursementTransactionId = outflow.Id;
        var start = DateTime.UtcNow.Date;
        foreach (var item in BuildSchedule(loan.RepaymentMonths, loan.TotalRepayable, loan.MonthlyRepayment, start,
                     (n, due, amt) => new MemberLoanRepayment { MemberLoanId = loan.Id, InstalmentNumber = n, DueDate = due, AmountDue = amt }))
            Db.MemberLoanRepayments.Add(item);

        await AuditAsync("MEMBER_LOAN_DISBURSED", $"Loan {loan.LoanNumber} of R{loan.Principal:N2} was disbursed.", groupId, nameof(MemberLoan), loan.Id, ct);
        await Db.SaveChangesAsync(ct);
        await dbTx.CommitAsync(ct);
        return await MapMemberLoanAsync(loan, ct);
    }

    public async Task RecordMemberRepaymentAsync(Guid groupId, Guid loanId, RecordRepaymentRequest request, CancellationToken ct = default)
    {
        var membership = await FindMembershipAsync(groupId, ct);
        var isOfficer = Current.IsPlatformAdmin || membership is { Role: GroupRole.StokvelAdministrator or GroupRole.Treasurer };
        var loan = await Db.MemberLoans.FirstOrDefaultAsync(l => l.Id == loanId && l.GroupId == groupId, ct)
                   ?? throw new NotFoundException("Member loan not found.");
        if (!isOfficer && membership?.Id != loan.MemberId)
            throw new ForbiddenException("You cannot record this repayment.");
        if (loan.Status is not (LoanStatus.Active or LoanStatus.PartiallyRepaid or LoanStatus.Disbursed))
            throw new BusinessRuleException("INVALID_STATE", "This loan is not in repayment.");

        var remaining = request.Amount;
        var instalments = await Db.MemberLoanRepayments.Where(r => r.MemberLoanId == loan.Id).OrderBy(r => r.InstalmentNumber).ToListAsync(ct);
        foreach (var row in instalments)
        {
            if (remaining <= 0) break;
            var due = row.AmountDue - row.AmountPaid;
            if (due <= 0) continue;
            var pay = Math.Min(due, remaining);
            row.AmountPaid += pay;
            remaining -= pay;
            if (row.AmountPaid >= row.AmountDue)
            {
                row.Status = ContributionStatus.Paid;
                row.PaidDate = request.PaymentDate.UtcDateTime.Date;
            }
            else row.Status = ContributionStatus.PartiallyPaid;
        }

        var capital = await GetAccountAsync(groupId, AccountType.GroupCapital, null, ct);
        var receivable = await GetAccountAsync(groupId, AccountType.LoanReceivable, null, ct);
        await using var dbTx = await Db.Database.BeginTransactionAsync(ct);
        await PostAsync(groupId, capital.Id, loan.MemberId, TransactionType.LoanRepayment, TransactionDirection.Inflow,
            request.Amount, TransactionStatus.Confirmed, request.PaymentReference, request.PaymentMethod, $"Repayment {loan.LoanNumber}", loan.Id, null, ct);
        var recvBal = await ConfirmedBalanceAsync(receivable.Id, ct);
        var reduce = Math.Min(request.Amount, Math.Max(recvBal, 0));
        if (reduce > 0)
            await PostAsync(groupId, receivable.Id, loan.MemberId, TransactionType.LoanRepayment, TransactionDirection.Outflow,
                reduce, TransactionStatus.Confirmed, request.PaymentReference, request.PaymentMethod, $"Receivable reduction {loan.LoanNumber}", loan.Id, null, ct);

        var paid = instalments.Sum(i => i.AmountPaid);
        loan.Status = paid >= loan.TotalRepayable ? LoanStatus.FullyRepaid : LoanStatus.PartiallyRepaid;
        await AuditAsync("MEMBER_REPAYMENT", $"Repayment of R{request.Amount:N2} recorded for loan {loan.LoanNumber}.", groupId, nameof(MemberLoan), loan.Id, ct);
        await Db.SaveChangesAsync(ct);
        await dbTx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<MemberLoanDto>> ListMemberLoansAsync(Guid groupId, Guid? memberId, CancellationToken ct = default)
    {
        await EnsureGroupAccessAsync(groupId, ct);
        var query = Db.MemberLoans.Where(l => l.GroupId == groupId);
        if (memberId.HasValue) query = query.Where(l => l.MemberId == memberId);
        var loans = await query.OrderByDescending(l => l.CreatedAt).ToListAsync(ct);
        var list = new List<MemberLoanDto>();
        foreach (var l in loans) list.Add(await MapMemberLoanAsync(l, ct));
        return list;
    }

    public async Task<MemberLoanDto> GetMemberLoanAsync(Guid loanId, CancellationToken ct = default)
    {
        var loan = await Db.MemberLoans.FirstOrDefaultAsync(l => l.Id == loanId, ct) ?? throw new NotFoundException("Loan not found.");
        await EnsureGroupAccessAsync(loan.GroupId, ct);
        return await MapMemberLoanAsync(loan, ct);
    }

    public async Task<IReadOnlyList<LoanRepaymentDto>> MemberScheduleAsync(Guid loanId, CancellationToken ct = default)
    {
        var loan = await Db.MemberLoans.FirstOrDefaultAsync(l => l.Id == loanId, ct) ?? throw new NotFoundException("Loan not found.");
        await EnsureGroupAccessAsync(loan.GroupId, ct);
        return await Db.MemberLoanRepayments.Where(r => r.MemberLoanId == loanId).OrderBy(r => r.InstalmentNumber)
            .Select(r => new LoanRepaymentDto(r.Id, r.InstalmentNumber, r.DueDate, r.AmountDue, r.AmountPaid, r.PaidDate, r.Status))
            .ToListAsync(ct);
    }

    private async Task<decimal> MemberContributionsAsync(Guid memberId, CancellationToken ct)
    {
        var account = await Db.FinancialAccounts.FirstOrDefaultAsync(a => a.MemberId == memberId && a.Type == AccountType.MemberContribution, ct);
        return account is null ? 0 : await ConfirmedBalanceAsync(account.Id, ct);
    }

    private async Task<GroupLoan> LoadGroupLoanAsync(Guid groupId, Guid loanId, CancellationToken ct) =>
        await Db.GroupLoans.FirstOrDefaultAsync(l => l.Id == loanId && l.GroupId == groupId, ct)
        ?? throw new NotFoundException("Group loan not found.");

    private async Task EnsureCanDecideAsync(Guid groupId, string requiredRole, CancellationToken ct)
    {
        if (requiredRole == SystemRoles.PlatformAdmin)
        {
            if (!Current.IsPlatformAdmin)
                throw new ForbiddenException("This approval step requires the Platform Administrator.");
            return;
        }

        if (Current.IsPlatformAdmin) return;
        var member = await FindMembershipAsync(groupId, ct) ?? throw new ForbiddenException("Not a member of this Stokvel.");
        if (!Enum.TryParse<GroupRole>(requiredRole, out var role) || member.Role != role)
            throw new ForbiddenException($"This approval step requires the {requiredRole} role.");
    }

    private static List<(int Order, string Role)> BuildGroupApprovalSteps(LoanApprovalWorkflow workflow) => workflow switch
    {
        LoanApprovalWorkflow.TreasurerThenPlatformAdmin => new() { (1, nameof(GroupRole.Treasurer)), (2, SystemRoles.PlatformAdmin) },
        LoanApprovalWorkflow.CommitteeThenPlatformAdmin => new() { (1, nameof(GroupRole.Chairperson)), (2, nameof(GroupRole.Treasurer)), (3, SystemRoles.PlatformAdmin) },
        LoanApprovalWorkflow.PlatformAdminOnly => new() { (1, SystemRoles.PlatformAdmin) },
        _ => new() { (1, nameof(GroupRole.StokvelAdministrator)), (2, SystemRoles.PlatformAdmin) }
    };

    private async Task<GroupLoanDto> MapGroupLoanAsync(GroupLoan loan, CancellationToken ct)
    {
        var group = await Db.StokvelGroups.FindAsync(new object[] { loan.GroupId }, ct);
        var paid = await Db.GroupLoanRepayments.Where(r => r.GroupLoanId == loan.Id).SumAsync(r => r.AmountPaid, ct);
        var next = await Db.GroupLoanRepayments.Where(r => r.GroupLoanId == loan.Id && r.Status != ContributionStatus.Paid)
            .OrderBy(r => r.InstalmentNumber).FirstOrDefaultAsync(ct);
        return new GroupLoanDto(loan.Id, loan.LoanNumber, loan.GroupId, group?.Name ?? "—", loan.Principal, loan.InterestRatePercent,
            loan.TotalRepayable, loan.RepaymentMonths, loan.MonthlyRepayment, loan.Purpose, loan.Status, loan.Source,
            loan.RequestedDate, paid, Money.Round(loan.TotalRepayable - paid), next?.DueDate, next is null ? null : next.AmountDue - next.AmountPaid);
    }

    private async Task<MemberLoanDto> MapMemberLoanAsync(MemberLoan loan, CancellationToken ct)
    {
        var member = await Db.GroupMembers.FirstAsync(m => m.Id == loan.MemberId, ct);
        var user = await Users.FindByIdAsync(member.UserId.ToString());
        var paid = await Db.MemberLoanRepayments.Where(r => r.MemberLoanId == loan.Id).SumAsync(r => r.AmountPaid, ct);
        var next = await Db.MemberLoanRepayments.Where(r => r.MemberLoanId == loan.Id && r.Status != ContributionStatus.Paid)
            .OrderBy(r => r.InstalmentNumber).FirstOrDefaultAsync(ct);
        return new MemberLoanDto(loan.Id, loan.LoanNumber, loan.GroupId, loan.MemberId, user?.FullName ?? "—", loan.Principal,
            loan.InterestRatePercent, loan.TotalRepayable, loan.RepaymentMonths, loan.MonthlyRepayment, loan.Purpose, loan.Status,
            loan.RequestedDate, paid, Money.Round(loan.TotalRepayable - paid), next?.DueDate, next is null ? null : next.AmountDue - next.AmountPaid);
    }
}
