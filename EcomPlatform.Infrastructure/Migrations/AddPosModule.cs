using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcomPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPosModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── PosSessions ──────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "PosSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    TenantId = table.Column<Guid>(nullable: true),
                    CashierId = table.Column<Guid>(nullable: false),
                    TerminalName = table.Column<string>(maxLength: 50, nullable: false),
                    Status = table.Column<int>(nullable: false, defaultValue: 1),
                    OpeningCash = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClosingCash = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExpectedCash = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CashDifference = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OpenedAt = table.Column<DateTime>(nullable: false),
                    ClosedAt = table.Column<DateTime>(nullable: true),
                    TotalSales = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCashSales = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCardSales = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalRefunds = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OrdersCount = table.Column<int>(nullable: false),
                    Notes = table.Column<string>(maxLength: 500, nullable: false, defaultValue: ""),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosSessions", x => x.Id);
                    table.ForeignKey("FK_PosSessions_Tenants_TenantId",
                        x => x.TenantId, "Tenants", "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey("FK_PosSessions_Users_CashierId",
                        x => x.CashierId, "Users", "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex("IX_PosSessions_TenantId",
                "PosSessions", "TenantId");
            migrationBuilder.CreateIndex("IX_PosSessions_CashierId",
                "PosSessions", "CashierId");
            migrationBuilder.CreateIndex("IX_PosSessions_Status",
                "PosSessions", "Status");

            // ── PosOrders ────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "PosOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    TenantId = table.Column<Guid>(nullable: true),
                    PosSessionId = table.Column<Guid>(nullable: false),
                    ReceiptNumber = table.Column<string>(maxLength: 50, nullable: false),
                    Status = table.Column<int>(nullable: false, defaultValue: 1),
                    CustomerId = table.Column<Guid>(nullable: true),
                    CustomerName = table.Column<string>(maxLength: 100, nullable: false, defaultValue: ""),
                    CustomerPhone = table.Column<string>(maxLength: 20, nullable: false, defaultValue: ""),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CashPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CardPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Change = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<int>(nullable: false, defaultValue: 1),
                    CouponCode = table.Column<string>(maxLength: 50, nullable: true),
                    Notes = table.Column<string>(maxLength: 500, nullable: false, defaultValue: ""),
                    CompletedAt = table.Column<DateTime>(nullable: true),
                    LinkedOrderId = table.Column<Guid>(nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosOrders", x => x.Id);
                    table.ForeignKey("FK_PosOrders_PosSessions_PosSessionId",
                        x => x.PosSessionId, "PosSessions", "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_PosOrders_Tenants_TenantId",
                        x => x.TenantId, "Tenants", "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey("FK_PosOrders_Customers_CustomerId",
                        x => x.CustomerId, "Customers", "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex("IX_PosOrders_PosSessionId",
                "PosOrders", "PosSessionId");
            migrationBuilder.CreateIndex("IX_PosOrders_TenantId",
                "PosOrders", "TenantId");
            migrationBuilder.CreateIndex("IX_PosOrders_ReceiptNumber",
                "PosOrders", "ReceiptNumber");
            migrationBuilder.CreateIndex("IX_PosOrders_Status",
                "PosOrders", "Status");

            // ── PosOrderItems ────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "PosOrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    PosOrderId = table.Column<Guid>(nullable: false),
                    ProductId = table.Column<Guid>(nullable: false),
                    ProductName = table.Column<string>(maxLength: 200, nullable: false),
                    ProductSKU = table.Column<string>(maxLength: 100, nullable: false, defaultValue: ""),
                    ProductBarcode = table.Column<string>(maxLength: 100, nullable: false, defaultValue: ""),
                    ProductImage = table.Column<string>(maxLength: 500, nullable: false, defaultValue: ""),
                    Quantity = table.Column<int>(nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosOrderItems", x => x.Id);
                    table.ForeignKey("FK_PosOrderItems_PosOrders_PosOrderId",
                        x => x.PosOrderId, "PosOrders", "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_PosOrderItems_Products_ProductId",
                        x => x.ProductId, "Products", "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex("IX_PosOrderItems_PosOrderId",
                "PosOrderItems", "PosOrderId");
            migrationBuilder.CreateIndex("IX_PosOrderItems_ProductId",
                "PosOrderItems", "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("PosOrderItems");
            migrationBuilder.DropTable("PosOrders");
            migrationBuilder.DropTable("PosSessions");
        }
    }
}
