namespace EcomPlatform.Application.DTOs.Tenants
{
    public class UpdateTenantDto
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Logo { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public DateTime? SubscriptionEndDate { get; set; }
    }
}