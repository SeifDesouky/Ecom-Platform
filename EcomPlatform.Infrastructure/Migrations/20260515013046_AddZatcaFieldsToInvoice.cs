using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcomPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddZatcaFieldsToInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ZatcaQrCode",
                table: "Invoices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ZatcaReportedAt",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZatcaStatus",
                table: "Invoices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZatcaUuid",
                table: "Invoices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZatcaXmlPath",
                table: "Invoices",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ZatcaQrCode",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ZatcaReportedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ZatcaStatus",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ZatcaUuid",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ZatcaXmlPath",
                table: "Invoices");
        }
    }
}
