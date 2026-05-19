using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcomPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddZatcaFieldsToInvoiceAndTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VatNumber",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VatNumber",
                table: "Tenants");
        }
    }
}
