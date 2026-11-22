using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ramii.dev.Data.Migrations;

/// <inheritdoc />
public partial class _20261122085920_InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Threads",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                VisitorName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                VisitorEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                LastActivityAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Threads", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Messages",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ThreadId = table.Column<int>(type: "int", nullable: false),
                Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                IsFromAdmin = table.Column<bool>(type: "bit", nullable: false),
                SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Messages", x => x.Id);
                table.ForeignKey(
                    name: "FK_Messages_Threads_ThreadId",
                    column: x => x.ThreadId,
                    principalTable: "Threads",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Messages_SentAt",
            table: "Messages",
            column: "SentAt");

        migrationBuilder.CreateIndex(
            name: "IX_Messages_ThreadId",
            table: "Messages",
            column: "ThreadId");

        migrationBuilder.CreateIndex(
            name: "IX_Threads_LastActivityAt",
            table: "Threads",
            column: "LastActivityAt");

        migrationBuilder.CreateIndex(
            name: "IX_Threads_Status",
            table: "Threads",
            column: "Status");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Messages");

        migrationBuilder.DropTable(
            name: "Threads");
    }
}
