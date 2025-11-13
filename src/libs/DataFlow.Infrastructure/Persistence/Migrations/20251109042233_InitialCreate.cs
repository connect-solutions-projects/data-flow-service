using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Guard for dev environments where the table may have been created by EnsureCreated
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"IngestionJobs\" CASCADE;");
            migrationBuilder.CreateTable(
                name: "IngestionJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<string>(type: "text", nullable: false),
                    FileType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    FileMetadata_Name = table.Column<string>(type: "text", nullable: false),
                    FileMetadata_Size = table.Column<long>(type: "bigint", nullable: false),
                    FileMetadata_ContentType = table.Column<string>(type: "text", nullable: false),
                    FileMetadata_Checksum = table.Column<string>(type: "text", nullable: false),
                    FileMetadata_UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidationResult = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestionJobs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IngestionJobs");
        }
    }
}
