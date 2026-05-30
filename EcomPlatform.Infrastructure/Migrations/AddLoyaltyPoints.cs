using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcomPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoyaltyPoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    TenantId = table.Column<Guid>(nullable: true),
                    CustomerId = table.Column<Guid>(nullable: false),
                    Type = table.Column<int>(nullable: false),
                    Points = table.Column<int>(nullable: false),
                    BalanceAfter = table.Column<int>(nullable: false),
                    Reference = table.Column<string>(maxLength: 200, nullable: false, defaultValue: ""),
                    Notes = table.Column<string>(maxLength: 500, nullable: false, defaultValue: ""),
                    ExpiresAt = table.Column<DateTime>(nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyPoints", x => x.Id);

                    table.ForeignKey(
                        name: "FK_LoyaltyPoints_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);

                    table.ForeignKey(
                        name: "FK_LoyaltyPoints_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPoints_TenantId",
                table: "LoyaltyPoints",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPoints_CustomerId",
                table: "LoyaltyPoints",
                column: "CustomerId");

            // الأهم — البحث بالمرجع لمنع التكرار
            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPoints_Reference",
                table: "LoyaltyPoints",
                column: "Reference");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPoints_ExpiresAt",
                table: "LoyaltyPoints",
                column: "ExpiresAt",
                filter: "[ExpiresAt] IS NOT NULL");

            // ── Settings seed: القيم الافتراضية لإعدادات الـ Loyalty ──────────
            // (استبدل tenantId بـ NULL لو الإعدادات Global)
            // loyalty_enabled         = "false"
            // loyalty_earn_per_amount = "10"
            // loyalty_points_per_unit = "1"
            // loyalty_redeem_per_point= "0.05"
            // loyalty_min_redeem      = "100"
            // loyalty_expiry_days     = "365"
            //
            // أضفهم في AppDbContext.OnModelCreating أو في Seed data منفصل.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "LoyaltyPoints");
        }
    }
}
