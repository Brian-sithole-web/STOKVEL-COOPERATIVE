namespace Stokvel.Domain.Entities;

public class PlatformSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public decimal DefaultMaxGroupBorrowingPercent { get; set; } = 80m;
    public int DefaultMinimumMembers { get; set; } = BusinessConstants.MinimumMembers;
    public LendingSource DefaultLendingSources { get; set; } = LendingSource.Both;
    public bool SetupCompleted { get; set; }
    public DateTimeOffset? SetupCompletedAt { get; set; }
}
