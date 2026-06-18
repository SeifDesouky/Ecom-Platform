namespace EcomPlatform.Application.DTOs.Tenants
{
    public class UpdateTenantDto
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Logo { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ThemeColor { get; set; } = "#10B981";
        public DateTime? SubscriptionEndDate { get; set; }
    }
}