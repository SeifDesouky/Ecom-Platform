using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcomPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    TenantId = table.Column<Guid>(nullable: true),
                    ProductId = table.Column<Guid>(nullable: false),
                    CustomerId = table.Column<Guid>(nullable: true),
                    ReviewerName = table.Column<string>(maxLength: 100, nullable: false),
                    ReviewerEmail = table.Column<string>(maxLength: 150, nullable: false, defaultValue: ""),
                    Rating = table.Column<int>(nullable: false),
                    Title = table.Column<string>(maxLength: 200, nullable: false, defaultValue: ""),
                    Body = table.Column<string>(maxLength: 2000, nullable: false, defaultValue: ""),
                    Status = table.Column<int>(nullable: false, defaultValue: 1),
                    IsVerifiedPurchase = table.Column<bool>(nullable: false, defaultValue: false),
                    OwnerReply = table.Column<string>(maxLength: 1000, nullable: true),
                    OwnerRepliedAt = table.Column<DateTime>(nullable: true),
                    HelpfulCount = table.Column<int>(nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductReviews", x => x.Id);

                    table.ForeignKey(
                        name: "FK_ProductReviews_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);

                    table.ForeignKey(
                        name: "FK_ProductReviews_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);

                    table.ForeignKey(
                        name: "FK_ProductReviews_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // ── Indexes ──────────────────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_ProductReviews_TenantId",
                table: "ProductReviews",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviews_ProductId",
                table: "ProductReviews",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviews_CustomerId",
                table: "ProductReviews",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviews_Status",
                table: "ProductReviews",
                column: "Status");

            // Unique: عميل واحد = تقييم واحد لكل منتج
            migrationBuilder.CreateIndex(
                name: "UX_ProductReviews_Customer_Product",
                table: "ProductReviews",
                columns: new[] { "CustomerId", "ProductId" },
                unique: true,
                filter: "[CustomerId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ProductReviews");
        }
    }
}