using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Budget.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSavingsGoalIsArchived : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_archived",
                table: "savings_goals",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_archived",
                table: "savings_goals");
        }
    }
}
