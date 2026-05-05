namespace EcomPlatform.Application.DTOs.Tenants
{
    public class CreateTenantDto
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Logo { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public DateTime? SubscriptionEndDate { get; set; }
    }
}