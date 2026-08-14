namespace Stokvel.Domain.Entities;

public class FinancialAccount : BaseEntity
{
    public Guid GroupId { get; set; }
    public Guid? MemberId { get; set; }
    public AccountType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = BusinessConstants.Currency;
    public bool IsActive { get; set; } = true;

    public StokvelGroup? Group { get; set; }
    public GroupMember? Member { get; set; }
    public ICollection<FinancialTransaction> Transactions { get; set; } = new List<FinancialTransaction>();
}
