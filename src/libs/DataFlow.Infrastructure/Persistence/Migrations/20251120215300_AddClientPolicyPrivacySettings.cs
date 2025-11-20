using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientPolicyPrivacySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RedactPayloadOnFailure",
                table: "ClientPolicies",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RedactPayloadOnSuccess",
                table: "ClientPolicies",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionDays",
                table: "ClientPolicies",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RedactPayloadOnFailure",
                table: "ClientPolicies");

            migrationBuilder.DropColumn(
                name: "RedactPayloadOnSuccess",
                table: "ClientPolicies");

            migrationBuilder.DropColumn(
                name: "RetentionDays",
                table: "ClientPolicies");
        }
    }
}
