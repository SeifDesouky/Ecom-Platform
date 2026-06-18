using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcomPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantDescriptionAndThemeColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Tenants",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThemeColor",
                table: "Tenants",
                type: "longtext",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ThemeColor",
                table: "Tenants");
        }
    }
}
