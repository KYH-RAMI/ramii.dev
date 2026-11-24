using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ramii.dev.Data.Migrations;

/// <inheritdoc />
public partial class _20261124182606_AddEmailAndResetToAdminUser : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Email",
            table: "AdminUsers",
            type: "nvarchar(256)",
            maxLength: 256,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<DateTime>(
            name: "ResetTokenExpiresAt",
            table: "AdminUsers",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ResetTokenHash",
            table: "AdminUsers",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Email",
            table: "AdminUsers");

        migrationBuilder.DropColumn(
            name: "ResetTokenExpiresAt",
            table: "AdminUsers");

        migrationBuilder.DropColumn(
            name: "ResetTokenHash",
            table: "AdminUsers");
    }
}
