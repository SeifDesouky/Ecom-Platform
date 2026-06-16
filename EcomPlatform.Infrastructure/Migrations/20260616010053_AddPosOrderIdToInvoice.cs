using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcomPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPosOrderIdToInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "OrderId",
                table: "Invoices",
                type: "char(36)",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "char(36)");

            migrationBuilder.AddColumn<Guid>(
                name: "PosOrderId",
                table: "Invoices",
                type: "char(36)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PosOrderId",
                table: "Invoices",
                column: "PosOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_PosOrders_PosOrderId",
                table: "Invoices",
                column: "PosOrderId",
                principalTable: "PosOrders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_PosOrders_PosOrderId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_PosOrderId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PosOrderId",
                table: "Invoices");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrderId",
                table: "Invoices",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "char(36)",
                oldNullable: true);
        }
    }
}
