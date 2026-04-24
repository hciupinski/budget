using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Budget.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "budget_projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archived_by = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_projects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "project_milestones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    completion_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    completion_source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archived_by = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_milestones", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_milestones_budget_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "budget_projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    milestone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    completion_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    completion_source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archived_by = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_steps", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_steps_project_milestones_milestone_id",
                        column: x => x.milestone_id,
                        principalTable: "project_milestones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    planned_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    manual_adjustment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    is_done = table.Column<bool>(type: "boolean", nullable: false),
                    done_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archived_by = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_items_project_steps_step_id",
                        column: x => x.step_id,
                        principalTable: "project_steps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    payment_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archived_by = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_payments", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_payments_project_items_item_id",
                        column: x => x.item_id,
                        principalTable: "project_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    original_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    stored_relative_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    is_removed = table.Column<bool>(type: "boolean", nullable: false),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_by = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_attachments_project_items_item_id",
                        column: x => x.item_id,
                        principalTable: "project_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_attachments_project_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "project_payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_budget_projects_is_archived_sort_order_name",
                table: "budget_projects",
                columns: new[] { "is_archived", "sort_order", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_project_attachments_item_id_is_removed_kind_created_at",
                table: "project_attachments",
                columns: new[] { "item_id", "is_removed", "kind", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_project_attachments_payment_id",
                table: "project_attachments",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_items_step_id_is_archived_is_done_sort_order",
                table: "project_items",
                columns: new[] { "step_id", "is_archived", "is_done", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_project_milestones_project_id_is_archived_completion_status~",
                table: "project_milestones",
                columns: new[] { "project_id", "is_archived", "completion_status", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_project_payments_item_id_is_archived_payment_date",
                table: "project_payments",
                columns: new[] { "item_id", "is_archived", "payment_date" });

            migrationBuilder.CreateIndex(
                name: "IX_project_steps_milestone_id_is_archived_completion_status_so~",
                table: "project_steps",
                columns: new[] { "milestone_id", "is_archived", "completion_status", "sort_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_attachments");

            migrationBuilder.DropTable(
                name: "project_payments");

            migrationBuilder.DropTable(
                name: "project_items");

            migrationBuilder.DropTable(
                name: "project_steps");

            migrationBuilder.DropTable(
                name: "project_milestones");

            migrationBuilder.DropTable(
                name: "budget_projects");
        }
    }
}
