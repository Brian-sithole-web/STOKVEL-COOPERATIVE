namespace Stokvel.Domain.Entities;

public class FinancialTransaction : BaseEntity
{
    public string TransactionNumber { get; set; } = string.Empty;
    public Guid GroupId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? MemberId { get; set; }
    public TransactionType Type { get; set; }
    public TransactionDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = BusinessConstants.Currency;
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public string? PaymentReference { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? Description { get; set; }
    public Guid? RelatedLoanId { get; set; }
    public Guid? RelatedContributionId { get; set; }
    public Guid? ReversesTransactionId { get; set; }
    public Guid RecordedByUserId { get; set; }

    public StokvelGroup? Group { get; set; }
    public FinancialAccount? Account { get; set; }
}
