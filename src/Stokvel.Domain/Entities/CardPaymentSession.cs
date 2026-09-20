namespace Stokvel.Domain.Entities;

public class CardPaymentSession : BaseEntity
{
    public Guid GroupId { get; set; }
    public Guid ContributionId { get; set; }
    public Guid MemberId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public CardPaymentStatus Status { get; set; } = CardPaymentStatus.Created;
    public string ChallengeToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ChallengedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? IssuerName { get; set; }
}
