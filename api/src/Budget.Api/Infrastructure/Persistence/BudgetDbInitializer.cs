using Budget.Api.Modules.Budget.Domain;
using Microsoft.EntityFrameworkCore;

namespace Budget.Api.Infrastructure.Persistence;

public sealed class BudgetDbInitializer(BudgetDbContext dbContext, ILogger<BudgetDbInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureUiStateSchemaAsync(cancellationToken);
        await EnsureEpic4SchemaAsync(cancellationToken);

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

    private async Task EnsureEpic4SchemaAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS budget_accounts (
                id uuid PRIMARY KEY,
                name character varying(140) NOT NULL,
                kind character varying(40) NOT NULL,
                currency character varying(10) NOT NULL,
                current_balance numeric(18,2) NOT NULL,
                is_archived boolean NOT NULL DEFAULT false,
                created_at timestamp with time zone NOT NULL,
                updated_at timestamp with time zone NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_budget_accounts_name ON budget_accounts (name);
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS account_transfers (
                id uuid PRIMARY KEY,
                from_account_id uuid NOT NULL REFERENCES budget_accounts(id) ON DELETE RESTRICT,
                to_account_id uuid NOT NULL REFERENCES budget_accounts(id) ON DELETE RESTRICT,
                amount numeric(18,2) NOT NULL,
                note character varying(280) NOT NULL,
                transfer_date timestamp with time zone NOT NULL,
                created_at timestamp with time zone NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_account_transfers_transfer_date ON account_transfers (transfer_date);
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS account_snapshots (
                id uuid PRIMARY KEY,
                account_id uuid NOT NULL REFERENCES budget_accounts(id) ON DELETE CASCADE,
                year integer NOT NULL,
                month integer NOT NULL,
                planned_balance numeric(18,2) NOT NULL,
                actual_balance numeric(18,2) NULL,
                updated_at timestamp with time zone NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_account_snapshots_unique ON account_snapshots (account_id, year, month);
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS investment_holdings (
                id uuid PRIMARY KEY,
                account_id uuid NOT NULL REFERENCES budget_accounts(id) ON DELETE CASCADE,
                symbol character varying(20) NOT NULL,
                units numeric(18,6) NOT NULL,
                average_cost numeric(18,4) NOT NULL,
                manual_price_override numeric(18,4) NULL,
                last_fetched_price numeric(18,4) NOT NULL,
                last_price_updated_at timestamp with time zone NOT NULL,
                updated_at timestamp with time zone NOT NULL
            );
            DROP INDEX IF EXISTS ix_investment_holdings_unique;
            CREATE INDEX IF NOT EXISTS ix_investment_holdings_account_symbol ON investment_holdings (account_id, symbol);
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS savings_goals (
                id uuid PRIMARY KEY,
                name character varying(160) NOT NULL,
                account_id uuid NULL REFERENCES budget_accounts(id) ON DELETE SET NULL,
                target_amount numeric(18,2) NOT NULL,
                current_amount numeric(18,2) NOT NULL,
                monthly_contribution_target numeric(18,2) NOT NULL,
                target_year integer NULL,
                target_month integer NULL,
                created_at timestamp with time zone NOT NULL,
                updated_at timestamp with time zone NOT NULL
            );
            """,
            cancellationToken);
    }
}
