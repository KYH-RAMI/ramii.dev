using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ramii.dev.Data.Migrations;

/// <inheritdoc />
public partial class _20261127164844_FixExistingMessageSourceClassification : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Klassificera befintliga adminsvar (IsFromAdmin=true) som AdminReply (2)
        // Befintliga formulärmeddelanden (IsFromAdmin=false) klassificeras redan som ContactForm (0) från tidigare migrering
        migrationBuilder.Sql(
            "UPDATE Messages SET [Source] = 2 WHERE IsFromAdmin = 1");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Återställ alla till default ContactForm (0) vid rollback
        migrationBuilder.Sql(
            "UPDATE Messages SET [Source] = 0");
    }
}
