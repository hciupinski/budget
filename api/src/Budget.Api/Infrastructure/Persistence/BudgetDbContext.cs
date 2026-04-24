using Budget.Api.Modules.Budget.Domain;
using Budget.Api.Modules.Budget.Domain.Projects;
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
    public DbSet<BudgetProject> Projects => Set<BudgetProject>();
    public DbSet<ProjectMilestone> ProjectMilestones => Set<ProjectMilestone>();
    public DbSet<ProjectStep> ProjectSteps => Set<ProjectStep>();
    public DbSet<ProjectCostItem> ProjectItems => Set<ProjectCostItem>();
    public DbSet<ProjectPayment> ProjectPayments => Set<ProjectPayment>();
    public DbSet<ProjectAttachment> ProjectAttachments => Set<ProjectAttachment>();

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

        modelBuilder.Entity<BudgetProject>(entity =>
        {
            entity.ToTable("budget_projects");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(180).IsRequired();
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(10).IsRequired();
            entity.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
            entity.Property(x => x.IsArchived).HasColumnName("is_archived").IsRequired();
            entity.Property(x => x.ArchivedAt).HasColumnName("archived_at");
            entity.Property(x => x.ArchivedBy).HasColumnName("archived_by").HasMaxLength(180);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasIndex(x => new { x.IsArchived, x.SortOrder, x.Name });
        });

        modelBuilder.Entity<ProjectMilestone>(entity =>
        {
            entity.ToTable("project_milestones");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(220).IsRequired();
            entity.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
            entity.Property(x => x.CompletionStatus).HasColumnName("completion_status").HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(x => x.CompletionSource).HasColumnName("completion_source").HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(x => x.CompletedAt).HasColumnName("completed_at");
            entity.Property(x => x.IsArchived).HasColumnName("is_archived").IsRequired();
            entity.Property(x => x.ArchivedAt).HasColumnName("archived_at");
            entity.Property(x => x.ArchivedBy).HasColumnName("archived_by").HasMaxLength(180);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.IsArchived, x.CompletionStatus, x.SortOrder });
            entity.HasOne(x => x.Project)
                .WithMany(x => x.Milestones)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectStep>(entity =>
        {
            entity.ToTable("project_steps");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.MilestoneId).HasColumnName("milestone_id").IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(220).IsRequired();
            entity.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
            entity.Property(x => x.CompletionStatus).HasColumnName("completion_status").HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(x => x.CompletionSource).HasColumnName("completion_source").HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(x => x.CompletedAt).HasColumnName("completed_at");
            entity.Property(x => x.IsArchived).HasColumnName("is_archived").IsRequired();
            entity.Property(x => x.ArchivedAt).HasColumnName("archived_at");
            entity.Property(x => x.ArchivedBy).HasColumnName("archived_by").HasMaxLength(180);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasIndex(x => new { x.MilestoneId, x.IsArchived, x.CompletionStatus, x.SortOrder });
            entity.HasOne(x => x.Milestone)
                .WithMany(x => x.Steps)
                .HasForeignKey(x => x.MilestoneId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectCostItem>(entity =>
        {
            entity.ToTable("project_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.StepId).HasColumnName("step_id").IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(260).IsRequired();
            entity.Property(x => x.PlannedAmount).HasColumnName("planned_amount").HasPrecision(18, 2);
            entity.Property(x => x.ManualAdjustment).HasColumnName("manual_adjustment").HasPrecision(18, 2);
            entity.Property(x => x.IsDone).HasColumnName("is_done").IsRequired();
            entity.Property(x => x.DoneAt).HasColumnName("done_at");
            entity.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
            entity.Property(x => x.IsArchived).HasColumnName("is_archived").IsRequired();
            entity.Property(x => x.ArchivedAt).HasColumnName("archived_at");
            entity.Property(x => x.ArchivedBy).HasColumnName("archived_by").HasMaxLength(180);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasIndex(x => new { x.StepId, x.IsArchived, x.IsDone, x.SortOrder });
            entity.HasOne(x => x.Step)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.StepId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectPayment>(entity =>
        {
            entity.ToTable("project_payments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2);
            entity.Property(x => x.PaymentDate).HasColumnName("payment_date").IsRequired();
            entity.Property(x => x.Note).HasColumnName("note").HasMaxLength(400).IsRequired();
            entity.Property(x => x.IsArchived).HasColumnName("is_archived").IsRequired();
            entity.Property(x => x.ArchivedAt).HasColumnName("archived_at");
            entity.Property(x => x.ArchivedBy).HasColumnName("archived_by").HasMaxLength(180);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasIndex(x => new { x.ItemId, x.IsArchived, x.PaymentDate });
            entity.HasOne(x => x.Item)
                .WithMany(x => x.Payments)
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectAttachment>(entity =>
        {
            entity.ToTable("project_attachments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.PaymentId).HasColumnName("payment_id");
            entity.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(x => x.MimeType).HasColumnName("mime_type").HasMaxLength(120).IsRequired();
            entity.Property(x => x.SizeBytes).HasColumnName("size_bytes").IsRequired();
            entity.Property(x => x.OriginalName).HasColumnName("original_name").HasMaxLength(260).IsRequired();
            entity.Property(x => x.StoredRelativePath).HasColumnName("stored_relative_path").HasMaxLength(1024).IsRequired();
            entity.Property(x => x.IsRemoved).HasColumnName("is_removed").IsRequired();
            entity.Property(x => x.RemovedAt).HasColumnName("removed_at");
            entity.Property(x => x.RemovedBy).HasColumnName("removed_by").HasMaxLength(180);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasIndex(x => new { x.ItemId, x.IsRemoved, x.Kind, x.CreatedAt });
            entity.HasOne(x => x.Item)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Payment)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.PaymentId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
