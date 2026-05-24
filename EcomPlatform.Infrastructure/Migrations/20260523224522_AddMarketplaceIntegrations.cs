using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcomPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoreIntegrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Platform = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApiKey = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    ApiSecret = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    RefreshToken = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    StoreUrl = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    ExternalStoreId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    WebhookSecret = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    TokenExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    SyncDirection = table.Column<int>(type: "int", nullable: false),
                    SyncProducts = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SyncOrders = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SyncCustomers = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SyncInventory = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SyncPrices = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AutoSyncIntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastErrorMessage = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    ConsecutiveErrorCount = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreIntegrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoreIntegrations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SyncLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalRecords = table.Column<int>(type: "int", nullable: false),
                    SuccessCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DurationSeconds = table.Column<double>(type: "double", nullable: true),
                    ErrorMessage = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    Details = table.Column<string>(type: "longtext", nullable: true),
                    IsManual = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    StoreIntegrationId = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncLogs_StoreIntegrations_StoreIntegrationId",
                        column: x => x.StoreIntegrationId,
                        principalTable: "StoreIntegrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SyncLogs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "WebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    EventType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RawPayload = table.Column<string>(type: "longtext", nullable: false),
                    SourceIp = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: true),
                    Signature = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    IsVerified = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    ExternalEntityId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    StoreIntegrationId = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebhookEvents_StoreIntegrations_StoreIntegrationId",
                        column: x => x.StoreIntegrationId,
                        principalTable: "StoreIntegrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WebhookEvents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_StoreIntegrations_TenantId",
                table: "StoreIntegrations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreIntegrations_TenantId_Platform",
                table: "StoreIntegrations",
                columns: new[] { "TenantId", "Platform" });

            migrationBuilder.CreateIndex(
                name: "IX_StoreIntegrations_TenantId_Status",
                table: "StoreIntegrations",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncLogs_IntegrationId_Status",
                table: "SyncLogs",
                columns: new[] { "StoreIntegrationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncLogs_StoreIntegrationId",
                table: "SyncLogs",
                column: "StoreIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncLogs_TenantId",
                table: "SyncLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncLogs_TenantId_CreatedAt",
                table: "SyncLogs",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEvents_IntegrationId_EventType",
                table: "WebhookEvents",
                columns: new[] { "StoreIntegrationId", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEvents_IntegrationId_Status",
                table: "WebhookEvents",
                columns: new[] { "StoreIntegrationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEvents_StoreIntegrationId",
                table: "WebhookEvents",
                column: "StoreIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEvents_TenantId",
                table: "WebhookEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEvents_TenantId_CreatedAt",
                table: "WebhookEvents",
                columns: new[] { "TenantId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncLogs");

            migrationBuilder.DropTable(
                name: "WebhookEvents");

            migrationBuilder.DropTable(
                name: "StoreIntegrations");
        }
    }
}
