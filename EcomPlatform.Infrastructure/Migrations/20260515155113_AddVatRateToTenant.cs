using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcomPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVatRateToTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                table: "Tenants",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "Tenants");
        }
    }
}
