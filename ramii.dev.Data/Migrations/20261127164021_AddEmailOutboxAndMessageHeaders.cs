using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ramii.dev.Data.Migrations;

/// <inheritdoc />
public partial class _20261127164021_AddEmailOutboxAndMessageHeaders : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "InReplyToMessageId",
            table: "Messages",
            type: "nvarchar(255)",
            maxLength: 255,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MessageId",
            table: "Messages",
            type: "nvarchar(255)",
            maxLength: 255,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "References",
            table: "Messages",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Source",
            table: "Messages",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "OutboxItems",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ThreadMessageId = table.Column<int>(type: "int", nullable: false),
                RecipientEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                MessageId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                AttemptCount = table.Column<int>(type: "int", nullable: false),
                NextRetryAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                SentAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OutboxItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_OutboxItems_Messages_ThreadMessageId",
                    column: x => x.ThreadMessageId,
                    principalTable: "Messages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Messages_MessageId",
            table: "Messages",
            column: "MessageId",
            unique: true,
            filter: "[MessageId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Messages_Source",
            table: "Messages",
            column: "Source");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxItems_MessageId",
            table: "OutboxItems",
            column: "MessageId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OutboxItems_Status",
            table: "OutboxItems",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxItems_Status_NextRetryAt",
            table: "OutboxItems",
            columns: new[] { "Status", "NextRetryAt" });

        migrationBuilder.CreateIndex(
            name: "IX_OutboxItems_ThreadMessageId",
            table: "OutboxItems",
            column: "ThreadMessageId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OutboxItems");

        migrationBuilder.DropIndex(
            name: "IX_Messages_MessageId",
            table: "Messages");

        migrationBuilder.DropIndex(
            name: "IX_Messages_Source",
            table: "Messages");

        migrationBuilder.DropColumn(
            name: "InReplyToMessageId",
            table: "Messages");

        migrationBuilder.DropColumn(
            name: "MessageId",
            table: "Messages");

        migrationBuilder.DropColumn(
            name: "References",
            table: "Messages");

        migrationBuilder.DropColumn(
            name: "Source",
            table: "Messages");
    }
}
