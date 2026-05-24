using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcomPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalIdColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Products",
                type: "longtext",
                nullable: false);

            migrationBuilder.AddColumn<Guid>(
                name: "StoreIntegrationId",
                table: "Products",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Orders",
                type: "longtext",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "ExternalOrderNumber",
                table: "Orders",
                type: "longtext",
                nullable: false);

            migrationBuilder.AddColumn<Guid>(
                name: "StoreIntegrationId",
                table: "Orders",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "OrderItems",
                type: "longtext",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "ExternalProductId",
                table: "OrderItems",
                type: "longtext",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "StoreIntegrationId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ExternalOrderNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "StoreIntegrationId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ExternalProductId",
                table: "OrderItems");
        }
    }
}
