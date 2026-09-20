using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Stokvel.Domain.Entities;
using Stokvel.Infrastructure.Identity;

namespace Stokvel.Infrastructure.Persistence;

public class StokvelDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public StokvelDbContext(DbContextOptions<StokvelDbContext> options) : base(options) { }

    public DbSet<StokvelGroup> StokvelGroups => Set<StokvelGroup>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<GroupInvitation> GroupInvitations => Set<GroupInvitation>();
    public DbSet<GroupRule> GroupRules => Set<GroupRule>();
    public DbSet<FinancialAccount> FinancialAccounts => Set<FinancialAccount>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();
    public DbSet<ContributionSchedule> ContributionSchedules => Set<ContributionSchedule>();
    public DbSet<Contribution> Contributions => Set<Contribution>();
    public DbSet<GroupLoan> GroupLoans => Set<GroupLoan>();
    public DbSet<GroupLoanRepayment> GroupLoanRepayments => Set<GroupLoanRepayment>();
    public DbSet<GroupLoanApproval> GroupLoanApprovals => Set<GroupLoanApproval>();
    public DbSet<MemberLoan> MemberLoans => Set<MemberLoan>();
    public DbSet<MemberLoanRepayment> MemberLoanRepayments => Set<MemberLoanRepayment>();
    public DbSet<MemberLoanApproval> MemberLoanApprovals => Set<MemberLoanApproval>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppNotification> Notifications => Set<AppNotification>();
    public DbSet<CardPaymentSession> CardPaymentSessions => Set<CardPaymentSession>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<PlatformSettings> PlatformSettings => Set<PlatformSettings>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<StokvelGroup>(e =>
        {
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Code).HasMaxLength(40).IsRequired();
            e.HasOne(x => x.Rule).WithOne(x => x.Group).HasForeignKey<GroupRule>(x => x.GroupId);
        });

        builder.Entity<GroupMember>(e =>
        {
            e.HasIndex(x => new { x.GroupId, x.UserId }).IsUnique();
            e.HasOne(x => x.Group).WithMany(x => x.Members).HasForeignKey(x => x.GroupId);
        });

        builder.Entity<GroupInvitation>(e =>
        {
            e.HasIndex(x => x.Token).IsUnique();
            e.HasOne(x => x.Group).WithMany(x => x.Invitations).HasForeignKey(x => x.GroupId);
            e.Property(x => x.Email).HasMaxLength(256);
        });

        builder.Entity<GroupRule>(e =>
        {
            e.Property(x => x.InitialDepositAmount).HasPrecision(18, 2);
            e.Property(x => x.MonthlyInstalmentAmount).HasPrecision(18, 2);
            e.Property(x => x.LatePenaltyAmount).HasPrecision(18, 2);
            e.Property(x => x.MaxGroupBorrowingPercentOfSavings).HasPrecision(18, 2);
            e.Property(x => x.MemberLoanMultiplier).HasPrecision(18, 2);
            e.Property(x => x.MinimumSavingsBalance).HasPrecision(18, 2);
            e.Property(x => x.DefaultInterestRatePercent).HasPrecision(18, 2);
        });

        builder.Entity<FinancialAccount>(e =>
        {
            e.HasOne(x => x.Group).WithMany(x => x.Accounts).HasForeignKey(x => x.GroupId);
            e.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId);
            e.Property(x => x.Name).HasMaxLength(200);
        });

        builder.Entity<FinancialTransaction>(e =>
        {
            e.HasIndex(x => x.TransactionNumber).IsUnique();
            e.HasIndex(x => new { x.GroupId, x.Status, x.Type });
            e.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId);
            e.HasOne(x => x.Account).WithMany(x => x.Transactions).HasForeignKey(x => x.AccountId);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.TransactionNumber).HasMaxLength(40);
        });

        builder.Entity<ContributionSchedule>(e =>
        {
            e.HasIndex(x => new { x.GroupId, x.MemberId, x.Year, x.Month, x.IsGroupLevel }).IsUnique();
            e.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId);
            e.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId);
            e.Property(x => x.AmountDue).HasPrecision(18, 2);
            e.Property(x => x.AmountPaid).HasPrecision(18, 2);
            e.Property(x => x.PenaltyAmount).HasPrecision(18, 2);
        });

        builder.Entity<Contribution>(e =>
        {
            e.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId);
            e.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId);
            e.HasOne(x => x.Schedule).WithMany(x => x.Payments).HasForeignKey(x => x.ScheduleId);
            e.Property(x => x.Amount).HasPrecision(18, 2);
        });

        builder.Entity<GroupLoan>(e =>
        {
            e.HasIndex(x => x.LoanNumber).IsUnique();
            e.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId);
            e.Property(x => x.Principal).HasPrecision(18, 2);
            e.Property(x => x.InterestRatePercent).HasPrecision(18, 2);
            e.Property(x => x.TotalRepayable).HasPrecision(18, 2);
            e.Property(x => x.MonthlyRepayment).HasPrecision(18, 2);
        });

        builder.Entity<GroupLoanRepayment>(e =>
        {
            e.HasOne(x => x.Loan).WithMany(x => x.Repayments).HasForeignKey(x => x.GroupLoanId);
            e.Property(x => x.AmountDue).HasPrecision(18, 2);
            e.Property(x => x.AmountPaid).HasPrecision(18, 2);
        });

        builder.Entity<GroupLoanApproval>(e =>
        {
            e.HasOne(x => x.Loan).WithMany(x => x.Approvals).HasForeignKey(x => x.GroupLoanId);
        });

        builder.Entity<MemberLoan>(e =>
        {
            e.HasIndex(x => x.LoanNumber).IsUnique();
            e.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId);
            e.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId);
            e.Property(x => x.Principal).HasPrecision(18, 2);
            e.Property(x => x.InterestRatePercent).HasPrecision(18, 2);
            e.Property(x => x.TotalRepayable).HasPrecision(18, 2);
            e.Property(x => x.MonthlyRepayment).HasPrecision(18, 2);
        });

        builder.Entity<MemberLoanRepayment>(e =>
        {
            e.HasOne(x => x.Loan).WithMany(x => x.Repayments).HasForeignKey(x => x.MemberLoanId);
            e.Property(x => x.AmountDue).HasPrecision(18, 2);
            e.Property(x => x.AmountPaid).HasPrecision(18, 2);
        });

        builder.Entity<MemberLoanApproval>(e =>
        {
            e.HasOne(x => x.Loan).WithMany(x => x.Approvals).HasForeignKey(x => x.MemberLoanId);
        });

        builder.Entity<NumberSequence>(e =>
        {
            e.HasKey(x => x.Name);
        });

        builder.Entity<PlatformSettings>(e =>
        {
            e.Property(x => x.DefaultMaxGroupBorrowingPercent).HasPrecision(18, 2);
        });

        builder.Entity<CardPaymentSession>(e =>
        {
            e.HasIndex(x => x.ChallengeToken).IsUnique();
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.ChallengeToken).HasMaxLength(80).IsRequired();
            e.Property(x => x.IssuerName).HasMaxLength(80);
        });
    }
}
