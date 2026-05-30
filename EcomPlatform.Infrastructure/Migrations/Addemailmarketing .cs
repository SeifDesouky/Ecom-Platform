using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcomPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailMarketing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── MailingLists ─────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "MailingLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    TenantId = table.Column<Guid>(nullable: true),
                    Name = table.Column<string>(maxLength: 100, nullable: false),
                    Description = table.Column<string>(maxLength: 500, nullable: false, defaultValue: ""),
                    WelcomeEmailSubject = table.Column<string>(maxLength: 200, nullable: true),
                    WelcomeEmailBody = table.Column<string>(nullable: true),
                    IsActive = table.Column<bool>(nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailingLists", x => x.Id);
                    table.ForeignKey("FK_MailingLists_Tenants_TenantId",
                        x => x.TenantId, "Tenants", "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex("IX_MailingLists_TenantId", "MailingLists", "TenantId");

            // ── MailingListSubscribers ───────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "MailingListSubscribers",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    TenantId = table.Column<Guid>(nullable: true),
                    MailingListId = table.Column<Guid>(nullable: false),
                    Email = table.Column<string>(maxLength: 150, nullable: false),
                    FirstName = table.Column<string>(maxLength: 100, nullable: false, defaultValue: ""),
                    LastName = table.Column<string>(maxLength: 100, nullable: false, defaultValue: ""),
                    Phone = table.Column<string>(maxLength: 20, nullable: false, defaultValue: ""),
                    CustomerId = table.Column<Guid>(nullable: true),
                    Status = table.Column<int>(nullable: false, defaultValue: 1),
                    Source = table.Column<string>(maxLength: 50, nullable: false, defaultValue: "Manual"),
                    UnsubscribedAt = table.Column<DateTime>(nullable: true),
                    UnsubscribeToken = table.Column<string>(maxLength: 64, nullable: false),
                    CustomFields = table.Column<string>(nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailingListSubscribers", x => x.Id);
                    table.ForeignKey("FK_MailingListSubscribers_MailingLists_MailingListId",
                        x => x.MailingListId, "MailingLists", "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_MailingListSubscribers_Customers_CustomerId",
                        x => x.CustomerId, "Customers", "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex("IX_MailingListSubscribers_MailingListId",
                "MailingListSubscribers", "MailingListId");
            migrationBuilder.CreateIndex("IX_MailingListSubscribers_Status",
                "MailingListSubscribers", "Status");
            migrationBuilder.CreateIndex("IX_MailingListSubscribers_UnsubscribeToken",
                "MailingListSubscribers", "UnsubscribeToken");
            // منع تكرار نفس الإيميل في نفس القائمة
            migrationBuilder.CreateIndex("UX_MailingListSubscribers_Email_ListId",
                "MailingListSubscribers",
                new[] { "MailingListId", "Email" },
                unique: true);

            // ── Campaigns ────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    TenantId = table.Column<Guid>(nullable: true),
                    Name = table.Column<string>(maxLength: 200, nullable: false),
                    Subject = table.Column<string>(maxLength: 300, nullable: false),
                    PreviewText = table.Column<string>(maxLength: 200, nullable: false, defaultValue: ""),
                    FromName = table.Column<string>(maxLength: 100, nullable: false, defaultValue: ""),
                    FromEmail = table.Column<string>(maxLength: 150, nullable: false, defaultValue: ""),
                    HtmlBody = table.Column<string>(nullable: false, defaultValue: ""),
                    TextBody = table.Column<string>(nullable: false, defaultValue: ""),
                    Status = table.Column<int>(nullable: false, defaultValue: 1),
                    ScheduledAt = table.Column<DateTime>(nullable: true),
                    SentAt = table.Column<DateTime>(nullable: true),
                    TotalRecipients = table.Column<int>(nullable: false, defaultValue: 0),
                    SentCount = table.Column<int>(nullable: false, defaultValue: 0),
                    DeliveredCount = table.Column<int>(nullable: false, defaultValue: 0),
                    OpenedCount = table.Column<int>(nullable: false, defaultValue: 0),
                    ClickedCount = table.Column<int>(nullable: false, defaultValue: 0),
                    BouncedCount = table.Column<int>(nullable: false, defaultValue: 0),
                    UnsubscribedCount = table.Column<int>(nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                    table.ForeignKey("FK_Campaigns_Tenants_TenantId",
                        x => x.TenantId, "Tenants", "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex("IX_Campaigns_TenantId", "Campaigns", "TenantId");
            migrationBuilder.CreateIndex("IX_Campaigns_Status", "Campaigns", "Status");
            migrationBuilder.CreateIndex("IX_Campaigns_ScheduledAt",
                "Campaigns", "ScheduledAt",
                filter: "[ScheduledAt] IS NOT NULL");

            // ── CampaignMailingLists ─────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "CampaignMailingLists",
                columns: table => new
                {
                    CampaignId = table.Column<Guid>(nullable: false),
                    MailingListId = table.Column<Guid>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMailingLists",
                        x => new { x.CampaignId, x.MailingListId });
                    table.ForeignKey("FK_CML_Campaigns",
                        x => x.CampaignId, "Campaigns", "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_CML_MailingLists",
                        x => x.MailingListId, "MailingLists", "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ── CampaignRecipients ───────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "CampaignRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    CampaignId = table.Column<Guid>(nullable: false),
                    Email = table.Column<string>(maxLength: 150, nullable: false),
                    Name = table.Column<string>(maxLength: 200, nullable: false, defaultValue: ""),
                    Status = table.Column<int>(nullable: false, defaultValue: 1),
                    TrackingToken = table.Column<string>(maxLength: 64, nullable: false),
                    SentAt = table.Column<DateTime>(nullable: true),
                    OpenedAt = table.Column<DateTime>(nullable: true),
                    ClickedAt = table.Column<DateTime>(nullable: true),
                    BouncedAt = table.Column<DateTime>(nullable: true),
                    FailReason = table.Column<string>(maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignRecipients", x => x.Id);
                    table.ForeignKey("FK_CampaignRecipients_Campaigns_CampaignId",
                        x => x.CampaignId, "Campaigns", "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_CampaignRecipients_CampaignId",
                "CampaignRecipients", "CampaignId");
            migrationBuilder.CreateIndex("IX_CampaignRecipients_TrackingToken",
                "CampaignRecipients", "TrackingToken");
            migrationBuilder.CreateIndex("IX_CampaignRecipients_Status",
                "CampaignRecipients", "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("CampaignRecipients");
            migrationBuilder.DropTable("CampaignMailingLists");
            migrationBuilder.DropTable("Campaigns");
            migrationBuilder.DropTable("MailingListSubscribers");
            migrationBuilder.DropTable("MailingLists");
        }
    }
}