using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Budget.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ChangedBy = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "budget_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    current_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "budget_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Section = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "budget_ui_state",
                columns: table => new
                {
                    state_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    value = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_ui_state", x => x.state_key);
                });

            migrationBuilder.CreateTable(
                name: "account_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false),
                    planned_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    actual_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_account_snapshots_budget_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "budget_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "account_transfers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    note = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: false),
                    transfer_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_transfers", x => x.id);
                    table.ForeignKey(
                        name: "FK_account_transfers_budget_accounts_from_account_id",
                        column: x => x.from_account_id,
                        principalTable: "budget_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_account_transfers_budget_accounts_to_account_id",
                        column: x => x.to_account_id,
                        principalTable: "budget_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "investment_holdings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    units = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    average_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    manual_price_override = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    last_fetched_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    last_price_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investment_holdings", x => x.id);
                    table.ForeignKey(
                        name: "FK_investment_holdings_budget_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "budget_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "savings_goals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    current_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    monthly_contribution_target = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    target_year = table.Column<int>(type: "integer", nullable: true),
                    target_month = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_savings_goals", x => x.id);
                    table.ForeignKey(
                        name: "FK_savings_goals_budget_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "budget_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "annual_plan_cells",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    PlannedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_annual_plan_cells", x => x.Id);
                    table.ForeignKey(
                        name: "FK_annual_plan_cells_budget_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "budget_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "monthly_actions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    PlannedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ActualAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monthly_actions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_monthly_actions_budget_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "budget_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_snapshots_account_id_year_month",
                table: "account_snapshots",
                columns: new[] { "account_id", "year", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_transfers_from_account_id",
                table: "account_transfers",
                column: "from_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_account_transfers_to_account_id",
                table: "account_transfers",
                column: "to_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_account_transfers_transfer_date",
                table: "account_transfers",
                column: "transfer_date");

            migrationBuilder.CreateIndex(
                name: "IX_annual_plan_cells_CategoryId",
                table: "annual_plan_cells",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_annual_plan_cells_Year_Month_CategoryId",
                table: "annual_plan_cells",
                columns: new[] { "Year", "Month", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_ChangedAt",
                table: "audit_entries",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_budget_accounts_name",
                table: "budget_accounts",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_budget_categories_Section_SortOrder",
                table: "budget_categories",
                columns: new[] { "Section", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_investment_holdings_account_id_symbol",
                table: "investment_holdings",
                columns: new[] { "account_id", "symbol" });

            migrationBuilder.CreateIndex(
                name: "IX_monthly_actions_CategoryId",
                table: "monthly_actions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_monthly_actions_Year_Month_CategoryId",
                table: "monthly_actions",
                columns: new[] { "Year", "Month", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_savings_goals_account_id",
                table: "savings_goals",
                column: "account_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_snapshots");

            migrationBuilder.DropTable(
                name: "account_transfers");

            migrationBuilder.DropTable(
                name: "annual_plan_cells");

            migrationBuilder.DropTable(
                name: "audit_entries");

            migrationBuilder.DropTable(
                name: "budget_ui_state");

            migrationBuilder.DropTable(
                name: "investment_holdings");

            migrationBuilder.DropTable(
                name: "monthly_actions");

            migrationBuilder.DropTable(
                name: "savings_goals");

            migrationBuilder.DropTable(
                name: "budget_categories");

            migrationBuilder.DropTable(
                name: "budget_accounts");
        }
    }
}
