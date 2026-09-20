using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Stokvel.Application.Common;
using Stokvel.Application.Dtos;
using Stokvel.Domain;
using Stokvel.Domain.Entities;
using Stokvel.Infrastructure.Identity;
using Stokvel.Infrastructure.Persistence;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Stokvel.Infrastructure.Services;

internal static class Money
{
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed class TokenFactory
{
    private readonly IConfiguration _config;

    public TokenFactory(IConfiguration config) => _config = config;

    public DateTimeOffset ExpiresAt => DateTimeOffset.UtcNow.AddMinutes(_config.GetValue("Jwt:ExpiresMinutes", 480));

    public string Create(ApplicationUser user, bool isPlatformAdmin)
    {
        var key = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.FullName),
            new("name", user.FullName),
            new(ClaimTypes.Role, isPlatformAdmin ? SystemRoles.PlatformAdmin : SystemRoles.User)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: ExpiresAt.UtcDateTime,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public abstract class AppServiceBase
{
    protected readonly StokvelDbContext Db;
    protected readonly ICurrentUser Current;
    protected readonly UserManager<ApplicationUser> Users;

    protected AppServiceBase(StokvelDbContext db, ICurrentUser current, UserManager<ApplicationUser> users)
    {
        Db = db;
        Current = current;
        Users = users;
    }

    protected async Task<string> NextNumberAsync(string prefix, CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var name = $"{prefix}-{year}";
        var row = await Db.NumberSequences.FirstOrDefaultAsync(x => x.Name == name, ct);
        if (row is null)
        {
            row = new NumberSequence { Name = name, LastValue = 0 };
            Db.NumberSequences.Add(row);
        }

        row.LastValue++;
        return $"{prefix}-{year}-{row.LastValue:D4}";
    }

    protected async Task AuditAsync(string action, string description, Guid? groupId = null, string? entityType = null, Guid? entityId = null, CancellationToken ct = default)
    {
        Db.AuditLogs.Add(new AuditLog
        {
            GroupId = groupId,
            ActorUserId = Current.IsAuthenticated ? Current.UserId : null,
            Action = action,
            Description = description,
            EntityType = entityType,
            EntityId = entityId
        });
        await Task.CompletedTask;
    }

    protected void Notify(Guid userId, string title, string message, NotificationType type, Guid? groupId = null, Guid? invitationId = null)
    {
        Db.Notifications.Add(new AppNotification
        {
            UserId = userId,
            GroupId = groupId,
            Title = title,
            Message = message,
            Type = type,
            InvitationId = invitationId
        });
    }

    protected async Task NotifyGroupOfficersAsync(Guid groupId, string title, string message, NotificationType type, CancellationToken ct)
    {
        var officers = await Db.GroupMembers
            .Where(m => m.GroupId == groupId && m.Status == MembershipStatus.Active && m.Role != GroupRole.Member)
            .Select(m => m.UserId)
            .ToListAsync(ct);
        foreach (var id in officers.Distinct())
            Notify(id, title, message, type, groupId);
    }

    protected async Task NotifyPlatformAdminsAsync(string title, string message, NotificationType type, Guid? groupId, CancellationToken ct)
    {
        var admins = await Users.GetUsersInRoleAsync(SystemRoles.PlatformAdmin);
        foreach (var admin in admins)
            Notify(admin.Id, title, message, type, groupId);
    }

    protected async Task<GroupMember?> FindMembershipAsync(Guid groupId, CancellationToken ct) =>
        await Db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == Current.UserId && m.Status == MembershipStatus.Active, ct);

    protected async Task EnsureGroupAccessAsync(Guid groupId, CancellationToken ct)
    {
        if (Current.IsPlatformAdmin) return;
        var member = await FindMembershipAsync(groupId, ct);
        if (member is null)
            throw new ForbiddenException("You are not authorised to access this Stokvel.");
    }

    protected async Task<GroupMember> RequireGroupRoleAsync(Guid groupId, CancellationToken ct, params GroupRole[] roles)
    {
        if (Current.IsPlatformAdmin)
        {
            return await Db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == Current.UserId, ct)
                   ?? new GroupMember { GroupId = groupId, UserId = Current.UserId, Role = GroupRole.StokvelAdministrator, Status = MembershipStatus.Active };
        }

        var member = await FindMembershipAsync(groupId, ct)
                     ?? throw new ForbiddenException("You are not a member of this Stokvel.");
        if (roles.Length > 0 && !roles.Contains(member.Role))
            throw new ForbiddenException("You do not have the required group role for this action.");
        return member;
    }

    protected static void EnsureNotClosed(StokvelGroup group)
    {
        if (group.Status == GroupStatus.Closed)
            throw new BusinessRuleException("GROUP_CLOSED", "A closed Stokvel cannot perform this action.");
        if (group.Status == GroupStatus.Suspended)
            throw new BusinessRuleException("GROUP_SUSPENDED", "This Stokvel is suspended.");
    }

    protected async Task<StokvelGroup> GetGroupAsync(Guid groupId, CancellationToken ct) =>
        await Db.StokvelGroups.Include(g => g.Rule).FirstOrDefaultAsync(g => g.Id == groupId, ct)
        ?? throw new NotFoundException("Stokvel group not found.");

    protected async Task<FinancialAccount> GetAccountAsync(Guid groupId, AccountType type, Guid? memberId, CancellationToken ct)
    {
        var account = await Db.FinancialAccounts.FirstOrDefaultAsync(
            a => a.GroupId == groupId && a.Type == type && a.MemberId == memberId && a.IsActive, ct);
        if (account is null)
            throw new BusinessRuleException("ACCOUNT_MISSING", $"Financial account {type} was not found.");
        return account;
    }

    protected async Task<decimal> ConfirmedBalanceAsync(Guid accountId, CancellationToken ct)
    {
        var rows = await Db.FinancialTransactions
            .Where(t => t.AccountId == accountId && t.Status == TransactionStatus.Confirmed)
            .Select(t => new { t.Direction, t.Amount })
            .ToListAsync(ct);
        return Money.Round(rows.Sum(t => t.Direction == TransactionDirection.Inflow ? t.Amount : -t.Amount));
    }

    protected async Task<decimal> GroupSavingsAsync(Guid groupId, CancellationToken ct)
    {
        var account = await Db.FinancialAccounts.FirstAsync(a => a.GroupId == groupId && a.Type == AccountType.GroupCapital, ct);
        return await ConfirmedBalanceAsync(account.Id, ct);
    }

    protected async Task<FinancialTransaction> PostAsync(
        Guid groupId,
        Guid accountId,
        Guid? memberId,
        TransactionType type,
        TransactionDirection direction,
        decimal amount,
        TransactionStatus status,
        string? reference,
        PaymentMethod? method,
        string? description,
        Guid? loanId,
        Guid? contributionId,
        CancellationToken ct)
    {
        if (amount <= 0)
            throw new BusinessRuleException("INVALID_AMOUNT", "Amount must be greater than zero.");

        var tx = new FinancialTransaction
        {
            TransactionNumber = await NextNumberAsync("TX", ct),
            GroupId = groupId,
            AccountId = accountId,
            MemberId = memberId,
            Type = type,
            Direction = direction,
            Amount = Money.Round(amount),
            TransactionDate = DateTime.UtcNow,
            Status = status,
            ConfirmedAt = status == TransactionStatus.Confirmed ? DateTime.UtcNow : null,
            PaymentReference = reference,
            PaymentMethod = method,
            Description = description,
            RelatedLoanId = loanId,
            RelatedContributionId = contributionId,
            RecordedByUserId = Current.UserId
        };
        Db.FinancialTransactions.Add(tx);
        return tx;
    }

    protected async Task<IReadOnlyList<GroupMembershipDto>> MembershipsForAsync(Guid userId, CancellationToken ct)
    {
        return await (
            from m in Db.GroupMembers
            join g in Db.StokvelGroups on m.GroupId equals g.Id
            where m.UserId == userId && m.Status == MembershipStatus.Active
            select new GroupMembershipDto(g.Id, g.Name, m.Id, m.Role, m.Status)
        ).ToListAsync(ct);
    }

    protected async Task<AuthResponse> BuildAuthAsync(ApplicationUser user, CancellationToken ct, TokenFactory tokens)
    {
        var isAdmin = await Users.IsInRoleAsync(user, SystemRoles.PlatformAdmin);
        var memberships = await MembershipsForAsync(user.Id, ct);
        return new AuthResponse(
            tokens.Create(user, isAdmin),
            tokens.ExpiresAt,
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            isAdmin,
            user.MustChangePassword,
            memberships);
    }

    protected static (decimal Total, decimal Monthly) ComputeRepayment(decimal principal, decimal ratePercent, int months)
    {
        var total = Money.Round(principal * (1 + ratePercent / 100m));
        var monthly = Money.Round(total / months);
        return (total, monthly);
    }

    protected static List<T> BuildSchedule<T>(int months, decimal total, decimal monthly, DateTime start, Func<int, DateTime, decimal, T> factory)
    {
        var items = new List<T>();
        decimal allocated = 0;
        for (var i = 1; i <= months; i++)
        {
            var amount = i == months ? Money.Round(total - allocated) : monthly;
            allocated += amount;
            items.Add(factory(i, start.AddMonths(i), amount));
        }
        return items;
    }

    protected async Task RefreshInstalmentStatusesAsync(Guid groupId, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var rows = await Db.ContributionSchedules.Where(s => s.GroupId == groupId && (s.Status == ContributionStatus.Pending || s.Status == ContributionStatus.PartiallyPaid || s.Status == ContributionStatus.Overdue)).ToListAsync(ct);
        foreach (var row in rows)
        {
            if (row.AmountPaid >= row.AmountDue && row.AmountDue > 0)
            {
                row.Status = ContributionStatus.Paid;
                continue;
            }

            if (row.AmountPaid > 0 && row.AmountPaid < row.AmountDue)
                row.Status = ContributionStatus.PartiallyPaid;

            var graceEnd = row.DueDate.AddDays(row.Group?.Rule?.GracePeriodDays ?? 0);
            // Group may not be included; load rule separately if needed
            if (today > row.DueDate && row.Status != ContributionStatus.Paid && row.Status != ContributionStatus.Waived && row.Status != ContributionStatus.Cancelled)
                row.Status = ContributionStatus.Overdue;
        }
    }

    protected static string DisplayLoanStatus(IEnumerable<LoanStatus> statuses)
    {
        var list = statuses.ToList();
        if (list.Any(s => s == LoanStatus.Defaulted)) return "Defaulted";
        if (list.Any(s => s == LoanStatus.Active || s == LoanStatus.Disbursed || s == LoanStatus.PartiallyRepaid)) return "Active";
        if (list.Any(s => s == LoanStatus.Submitted || s == LoanStatus.UnderReview)) return "Under review";
        if (list.Any(s => s == LoanStatus.FullyRepaid)) return "Repaid";
        return "None";
    }
}
