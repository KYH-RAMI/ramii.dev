using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ramii.dev.Data.Migrations;

/// <inheritdoc />
public partial class _20261127164915_AddOutboxConcurrencyFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ProcessingBy",
            table: "OutboxItems",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ProcessingStartedAt",
            table: "OutboxItems",
            type: "datetime2",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_OutboxItems_Status_ProcessingBy_ProcessingStartedAt",
            table: "OutboxItems",
            columns: new[] { "Status", "ProcessingBy", "ProcessingStartedAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_OutboxItems_Status_ProcessingBy_ProcessingStartedAt",
            table: "OutboxItems");

        migrationBuilder.DropColumn(
            name: "ProcessingBy",
            table: "OutboxItems");

        migrationBuilder.DropColumn(
            name: "ProcessingStartedAt",
            table: "OutboxItems");
    }
}
