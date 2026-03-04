using Budget.Api.Modules.Budget.Domain;
using Microsoft.EntityFrameworkCore;

namespace Budget.Api.Infrastructure.Persistence;

public sealed class BudgetDbContext(DbContextOptions<BudgetDbContext> options) : DbContext(options)
{
    public DbSet<BudgetCategory> Categories => Set<BudgetCategory>();
    public DbSet<AnnualPlanCell> AnnualPlanCells => Set<AnnualPlanCell>();
    public DbSet<MonthlyAction> MonthlyActions => Set<MonthlyAction>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<BudgetUiStateEntry> UiStateEntries => Set<BudgetUiStateEntry>();
    public DbSet<BudgetAccount> Accounts => Set<BudgetAccount>();
    public DbSet<AccountTransfer> AccountTransfers => Set<AccountTransfer>();
    public DbSet<AccountSnapshot> AccountSnapshots => Set<AccountSnapshot>();
    public DbSet<InvestmentHolding> InvestmentHoldings => Set<InvestmentHolding>();
    public DbSet<SavingsGoal> SavingsGoals => Set<SavingsGoal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BudgetCategory>(entity =>
        {
            entity.ToTable("budget_categories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Section).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.HasIndex(x => new { x.Section, x.SortOrder }).IsUnique();
        });

        modelBuilder.Entity<AnnualPlanCell>(entity =>
        {
            entity.ToTable("annual_plan_cells");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PlannedAmount).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.Year, x.Month, x.CategoryId }).IsUnique();
            entity.HasOne(x => x.Category)
                .WithMany(x => x.AnnualPlanCells)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MonthlyAction>(entity =>
        {
            entity.ToTable("monthly_actions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PlannedAmount).HasPrecision(18, 2);
            entity.Property(x => x.ActualAmount).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.HasIndex(x => new { x.Year, x.Month, x.CategoryId }).IsUnique();
            entity.HasOne(x => x.Category)
                .WithMany(x => x.MonthlyActions)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditEntry>(entity =>
        {
            entity.ToTable("audit_entries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EntityType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.EventType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ChangedBy).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(x => x.ChangedAt);
        });

        modelBuilder.Entity<BudgetUiStateEntry>(entity =>
        {
            entity.ToTable("budget_ui_state");
            entity.HasKey(x => x.StateKey);
            entity.Property(x => x.StateKey).HasColumnName("state_key").HasMaxLength(80).IsRequired();
            entity.Property(x => x.Value).HasColumnName("value").HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        });

        modelBuilder.Entity<BudgetAccount>(entity =>
        {
            entity.ToTable("budget_accounts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(140).IsRequired();
            entity.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(10).IsRequired();
            entity.Property(x => x.CurrentBalance).HasColumnName("current_balance").HasPrecision(18, 2);
            entity.Property(x => x.IsArchived).HasColumnName("is_archived").IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasIndex(x => x.Name);
        });

        modelBuilder.Entity<AccountTransfer>(entity =>
        {
            entity.ToTable("account_transfers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.FromAccountId).HasColumnName("from_account_id").IsRequired();
            entity.Property(x => x.ToAccountId).HasColumnName("to_account_id").IsRequired();
            entity.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2);
            entity.Property(x => x.Note).HasColumnName("note").HasMaxLength(280).IsRequired();
            entity.Property(x => x.TransferDate).HasColumnName("transfer_date").IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.HasIndex(x => x.TransferDate);
            entity.HasOne(x => x.FromAccount)
                .WithMany(x => x.OutgoingTransfers)
                .HasForeignKey(x => x.FromAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ToAccount)
                .WithMany(x => x.IncomingTransfers)
                .HasForeignKey(x => x.ToAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AccountSnapshot>(entity =>
        {
            entity.ToTable("account_snapshots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.AccountId).HasColumnName("account_id").IsRequired();
            entity.Property(x => x.Year).HasColumnName("year").IsRequired();
            entity.Property(x => x.Month).HasColumnName("month").IsRequired();
            entity.Property(x => x.PlannedBalance).HasColumnName("planned_balance").HasPrecision(18, 2);
            entity.Property(x => x.ActualBalance).HasColumnName("actual_balance").HasPrecision(18, 2);
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasIndex(x => new { x.AccountId, x.Year, x.Month }).IsUnique();
            entity.HasOne(x => x.Account)
                .WithMany(x => x.Snapshots)
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InvestmentHolding>(entity =>
        {
            entity.ToTable("investment_holdings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.AccountId).HasColumnName("account_id").IsRequired();
            entity.Property(x => x.Symbol).HasColumnName("symbol").HasMaxLength(20).IsRequired();
            entity.Property(x => x.Units).HasColumnName("units").HasPrecision(18, 6);
            entity.Property(x => x.AverageCost).HasColumnName("average_cost").HasPrecision(18, 4);
            entity.Property(x => x.ManualPriceOverride).HasColumnName("manual_price_override").HasPrecision(18, 4);
            entity.Property(x => x.LastFetchedPrice).HasColumnName("last_fetched_price").HasPrecision(18, 4);
            entity.Property(x => x.LastPriceUpdatedAt).HasColumnName("last_price_updated_at").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasIndex(x => new { x.AccountId, x.Symbol });
            entity.HasOne(x => x.Account)
                .WithMany(x => x.Holdings)
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SavingsGoal>(entity =>
        {
            entity.ToTable("savings_goals");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            entity.Property(x => x.AccountId).HasColumnName("account_id");
            entity.Property(x => x.TargetAmount).HasColumnName("target_amount").HasPrecision(18, 2);
            entity.Property(x => x.CurrentAmount).HasColumnName("current_amount").HasPrecision(18, 2);
            entity.Property(x => x.MonthlyContributionTarget).HasColumnName("monthly_contribution_target").HasPrecision(18, 2);
            entity.Property(x => x.TargetYear).HasColumnName("target_year");
            entity.Property(x => x.TargetMonth).HasColumnName("target_month");
            entity.Property(x => x.IsArchived).HasColumnName("is_archived").IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasOne(x => x.Account)
                .WithMany(x => x.SavingsGoals)
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
