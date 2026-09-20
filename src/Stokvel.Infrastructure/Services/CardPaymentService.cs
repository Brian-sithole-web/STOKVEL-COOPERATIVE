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

public sealed class CardPaymentService : AppServiceBase, ICardPaymentService
{
    private readonly IContributionService _contributions;

    public CardPaymentService(
        StokvelDbContext db,
        ICurrentUser current,
        UserManager<ApplicationUser> users,
        IContributionService contributions) : base(db, current, users)
    {
        _contributions = contributions;
    }

    public async Task<FirstPaymentDto> GetFirstPaymentAsync(Guid groupId, CancellationToken ct = default)
    {
        await EnsureGroupAccessAsync(groupId, ct);
        var group = await GetGroupAsync(groupId, ct);
        var member = await FindMembershipAsync(groupId, ct)
                     ?? throw new ForbiddenException("You are not a member of this Stokvel.");
        var rule = group.Rule ?? throw new BusinessRuleException("RULE_MISSING", "Group rules are not configured.");
        var paidInitial = await Db.Contributions.AnyAsync(
            c => c.GroupId == groupId && c.MemberId == member.Id && c.Kind == ContributionKind.InitialDeposit && c.Status == TransactionStatus.Confirmed,
            ct);
        var kind = paidInitial ? ContributionKind.MonthlyInstalment : ContributionKind.InitialDeposit;
        var amount = paidInitial ? rule.MonthlyInstalmentAmount : rule.InitialDepositAmount;
        return new FirstPaymentDto(group.Id, group.Name, member.Id, rule.InitialDepositAmount, rule.MonthlyInstalmentAmount, amount, kind, !paidInitial);
    }

    public async Task<CardPaymentDto> StartCheckoutAsync(Guid groupId, StartCardPaymentRequest request, CancellationToken ct = default)
    {
        await EnsureGroupAccessAsync(groupId, ct);
        var member = await FindMembershipAsync(groupId, ct)
                     ?? throw new ForbiddenException("You are not a member of this Stokvel.");
        if (request.Amount <= 0)
            throw new BusinessRuleException("INVALID_AMOUNT", "Enter an amount greater than zero.");

        var contribution = await _contributions.RecordAsync(groupId, new RecordContributionRequest(
            member.Id,
            null,
            request.Amount,
            request.Kind,
            DateTimeOffset.UtcNow,
            null,
            PaymentMethod.Card), ct);

        var session = new CardPaymentSession
        {
            GroupId = groupId,
            ContributionId = contribution.Id,
            MemberId = member.Id,
            UserId = Current.UserId,
            Amount = Money.Round(request.Amount),
            Status = CardPaymentStatus.Created,
            ChallengeToken = Convert.ToHexString(Guid.NewGuid().ToByteArray()) + Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };
        Db.CardPaymentSessions.Add(session);
        await AuditAsync("CARD_CHECKOUT_STARTED", $"Card checkout of R{session.Amount:N2} started.", groupId, nameof(CardPaymentSession), session.Id, ct);
        await Db.SaveChangesAsync(ct);
        return await MapAsync(session, contribution.Kind, ct);
    }

    public async Task<CardPaymentDto> GetAsync(Guid paymentId, CancellationToken ct = default)
    {
        var session = await GetOwnedSessionAsync(paymentId, ct);
        var contribution = await Db.Contributions.FirstAsync(c => c.Id == session.ContributionId, ct);
        return await MapAsync(session, contribution.Kind, ct);
    }

    public async Task<CardPaymentDto> BeginBankChallengeAsync(Guid paymentId, CancellationToken ct = default)
    {
        var session = await GetOwnedSessionAsync(paymentId, ct);
        EnsureUsable(session);
        session.Status = CardPaymentStatus.AwaitingBank;
        session.ChallengedAt = DateTime.UtcNow;
        session.IssuerName = "Issuing bank";
        await AuditAsync("CARD_3DS_CHALLENGE", "Bank authentication was requested for a card payment.", session.GroupId, nameof(CardPaymentSession), session.Id, ct);
        await Db.SaveChangesAsync(ct);
        var contribution = await Db.Contributions.FirstAsync(c => c.Id == session.ContributionId, ct);
        return await MapAsync(session, contribution.Kind, ct);
    }

    public async Task<CardPaymentDto> CompleteAsync(Guid paymentId, CompleteCardPaymentRequest request, CancellationToken ct = default)
    {
        var session = await GetOwnedSessionAsync(paymentId, ct);
        EnsureUsable(session);
        if (session.Status != CardPaymentStatus.AwaitingBank)
            throw new BusinessRuleException("CHALLENGE_REQUIRED", "Approve this payment in your banking app first.");

        var contribution = await Db.Contributions.FirstAsync(c => c.Id == session.ContributionId, ct);
        if (!request.Approved)
        {
            session.Status = CardPaymentStatus.Failed;
            session.CompletedAt = DateTime.UtcNow;
            await AuditAsync("CARD_PAYMENT_DECLINED", "The bank declined or the member cancelled 3-D Secure.", session.GroupId, nameof(CardPaymentSession), session.Id, ct);
            await Db.SaveChangesAsync(ct);
            return await MapAsync(session, contribution.Kind, ct);
        }

        var paymentReference = $"3DS-{session.Id.ToString("N")[..8].ToUpperInvariant()}";
        await _contributions.ConfirmFromGatewayAsync(session.GroupId, session.ContributionId, paymentReference, ct);
        session.Status = CardPaymentStatus.Succeeded;
        session.CompletedAt = DateTime.UtcNow;
        await AuditAsync("CARD_PAYMENT_SUCCEEDED", $"Bank-authenticated card payment {paymentReference} was confirmed.", session.GroupId, nameof(CardPaymentSession), session.Id, ct);
        await Db.SaveChangesAsync(ct);
        return await MapAsync(session, contribution.Kind, ct);
    }

    private async Task<CardPaymentSession> GetOwnedSessionAsync(Guid paymentId, CancellationToken ct)
    {
        var session = await Db.CardPaymentSessions.FirstOrDefaultAsync(row => row.Id == paymentId, ct)
                      ?? throw new NotFoundException("Payment session not found.");
        if (session.UserId != Current.UserId && !Current.IsPlatformAdmin)
            throw new ForbiddenException("You cannot access this payment.");
        return session;
    }

    private static void EnsureUsable(CardPaymentSession session)
    {
        if (session.Status is CardPaymentStatus.Succeeded or CardPaymentStatus.Failed or CardPaymentStatus.Expired)
            throw new BusinessRuleException("PAYMENT_CLOSED", "This payment is no longer open.");
        if (session.ExpiresAt < DateTime.UtcNow)
        {
            session.Status = CardPaymentStatus.Expired;
            throw new BusinessRuleException("PAYMENT_EXPIRED", "This card payment expired. Start a new payment.");
        }
    }

    private async Task<CardPaymentDto> MapAsync(CardPaymentSession session, ContributionKind kind, CancellationToken ct)
    {
        var group = await Db.StokvelGroups.AsNoTracking().FirstAsync(g => g.Id == session.GroupId, ct);
        return new CardPaymentDto(
            session.Id,
            session.GroupId,
            session.ContributionId,
            group.Name,
            session.Amount,
            kind,
            session.Status,
            $"/pay/{session.Id}",
            $"/pay/{session.Id}/bank",
            session.ExpiresAt,
            session.IssuerName);
    }
}
