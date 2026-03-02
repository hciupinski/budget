using Budget.Api.Modules.Budget.Domain;
using Microsoft.EntityFrameworkCore;

namespace Budget.Api.Infrastructure.Persistence;

public sealed class BudgetDbContext(DbContextOptions<BudgetDbContext> options) : DbContext(options)
{
    public DbSet<BudgetCategory> Categories => Set<BudgetCategory>();
    public DbSet<AnnualPlanCell> AnnualPlanCells => Set<AnnualPlanCell>();
    public DbSet<MonthlyAction> MonthlyActions => Set<MonthlyAction>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

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
    }
}
