using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcomPlatform.Infrastructure.Migrations
{
    public partial class AddPaymentLinksModule : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── PaymentLinks ──────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "PaymentLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    Code = table.Column<string>(maxLength: 20, nullable: false),
                    Title = table.Column<string>(maxLength: 200, nullable: false),
                    Description = table.Column<string>(maxLength: 1000, nullable: false, defaultValue: ""),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(maxLength: 10, nullable: false, defaultValue: "SAR"),
                    LinkType = table.Column<int>(nullable: false),
                    OrderId = table.Column<Guid>(nullable: true),
                    ExpiresAt = table.Column<DateTime>(nullable: true),
                    MaxUses = table.Column<int>(nullable: true),
                    UsedCount = table.Column<int>(nullable: false, defaultValue: 0),
                    Status = table.Column<int>(nullable: false, defaultValue: 1),
                    SuccessRedirectUrl = table.Column<string>(maxLength: 500, nullable: false, defaultValue: ""),
                    FailureRedirectUrl = table.Column<string>(maxLength: 500, nullable: false, defaultValue: ""),
                    Metadata = table.Column<string>(maxLength: 2000, nullable: false, defaultValue: ""),
                    CreatedById = table.Column<Guid>(nullable: true),
                    TenantId = table.Column<Guid>(nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentLinks_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaymentLinks_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaymentLinks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLinks_Code",
                table: "PaymentLinks",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLinks_TenantId_Status_CreatedAt",
                table: "PaymentLinks",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            // ── PaymentLinkItems ──────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "PaymentLinkItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    PaymentLinkId = table.Column<Guid>(nullable: false),
                    ProductId = table.Column<Guid>(nullable: false),
                    Quantity = table.Column<int>(nullable: false, defaultValue: 1),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProductName = table.Column<string>(maxLength: 300, nullable: false, defaultValue: ""),
                    CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentLinkItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentLinkItems_PaymentLinks_PaymentLinkId",
                        column: x => x.PaymentLinkId,
                        principalTable: "PaymentLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentLinkItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ── PaymentLinkTransactions ────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "PaymentLinkTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    PaymentLinkId = table.Column<Guid>(nullable: false),
                    PayerName = table.Column<string>(maxLength: 200, nullable: false, defaultValue: ""),
                    PayerEmail = table.Column<string>(maxLength: 200, nullable: false, defaultValue: ""),
                    PayerPhone = table.Column<string>(maxLength: 30, nullable: false, defaultValue: ""),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(maxLength: 10, nullable: false, defaultValue: "SAR"),
                    Status = table.Column<int>(nullable: false, defaultValue: 1),
                    GatewayName = table.Column<string>(maxLength: 100, nullable: false, defaultValue: ""),
                    GatewayTransactionId = table.Column<string>(maxLength: 200, nullable: false, defaultValue: ""),
                    GatewayResponse = table.Column<string>(maxLength: 4000, nullable: false, defaultValue: ""),
                    GeneratedOrderId = table.Column<Guid>(nullable: true),
                    PaidAt = table.Column<DateTime>(nullable: true),
                    FailureReason = table.Column<string>(maxLength: 500, nullable: false, defaultValue: ""),
                    TenantId = table.Column<Guid>(nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentLinkTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentLinkTransactions_PaymentLinks_PaymentLinkId",
                        column: x => x.PaymentLinkId,
                        principalTable: "PaymentLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentLinkTransactions_Orders_GeneratedOrderId",
                        column: x => x.GeneratedOrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaymentLinkTransactions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLinkTransactions_TenantId_Status_CreatedAt",
                table: "PaymentLinkTransactions",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLinkTransactions_GatewayTransactionId",
                table: "PaymentLinkTransactions",
                column: "GatewayTransactionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PaymentLinkTransactions");
            migrationBuilder.DropTable(name: "PaymentLinkItems");
            migrationBuilder.DropTable(name: "PaymentLinks");
        }
    }
}
