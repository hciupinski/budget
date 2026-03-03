using Budget.Api.Modules.Budget.Domain;
using Microsoft.EntityFrameworkCore;

namespace Budget.Api.Infrastructure.Persistence;

public sealed class BudgetDbInitializer(BudgetDbContext dbContext, ILogger<BudgetDbInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureUiStateSchemaAsync(cancellationToken);

        if (await dbContext.Categories.AnyAsync(cancellationToken))
        {
            return;
        }

        var categories = new[]
        {
            new BudgetCategory { Name = "Salary", Section = BudgetSection.Income, SortOrder = 10 },
            new BudgetCategory { Name = "JDG Income", Section = BudgetSection.Income, SortOrder = 20 },
            new BudgetCategory { Name = "Housing", Section = BudgetSection.Costs, SortOrder = 110 },
            new BudgetCategory { Name = "Utilities", Section = BudgetSection.Costs, SortOrder = 120 },
            new BudgetCategory { Name = "Food", Section = BudgetSection.Costs, SortOrder = 130 },
            new BudgetCategory { Name = "Transport", Section = BudgetSection.Costs, SortOrder = 140 },
            new BudgetCategory { Name = "Subscriptions", Section = BudgetSection.Costs, SortOrder = 150 },
            new BudgetCategory { Name = "Emergency Fund", Section = BudgetSection.SavingsInvestments, SortOrder = 210 },
            new BudgetCategory { Name = "Investments", Section = BudgetSection.SavingsInvestments, SortOrder = 220 }
        };

        dbContext.Categories.AddRange(categories);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Budget categories seeded: {Count}", categories.Length);
    }

    private async Task EnsureUiStateSchemaAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS budget_ui_state (
                state_key character varying(80) PRIMARY KEY,
                value jsonb NOT NULL,
                updated_at timestamp with time zone NOT NULL
            );
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = current_schema()
                      AND table_name = 'budget_ui_state'
                      AND column_name = 'Value'
                ) THEN
                    ALTER TABLE budget_ui_state RENAME COLUMN "Value" TO value;
                END IF;
            END $$;
            """,
            cancellationToken);
    }
}
