using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ramii.dev.Data.Migrations;

/// <inheritdoc />
public partial class _20261127185000_AddReservationTokenAndUniqueThreadMessageIdIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ReservationToken",
            table: "OutboxItems",
            type: "nvarchar(36)",
            maxLength: 36,
            nullable: true);
        migrationBuilder.DropIndex(
    name: "IX_OutboxItems_ThreadMessageId",
    table: "OutboxItems");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxItems_ThreadMessageId",
            table: "OutboxItems",
            column: "ThreadMessageId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_OutboxItems_ThreadMessageId",
            table: "OutboxItems");

        migrationBuilder.DropColumn(
            name: "ReservationToken",
            table: "OutboxItems");
    }
}
