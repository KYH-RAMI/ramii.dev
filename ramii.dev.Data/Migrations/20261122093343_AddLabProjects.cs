using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ramii.dev.Data.Migrations;

/// <inheritdoc />
public partial class _20261122093343_AddLabProjects : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LabProjects",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                TechTags = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                GitHubUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                DemoUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                SortOrder = table.Column<int>(type: "int", nullable: false),
                IsPublished = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LabProjects", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_LabProjects_IsPublished",
            table: "LabProjects",
            column: "IsPublished");

        migrationBuilder.CreateIndex(
            name: "IX_LabProjects_SortOrder",
            table: "LabProjects",
            column: "SortOrder");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "LabProjects");
    }
}
