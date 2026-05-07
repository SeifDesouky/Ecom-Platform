namespace EcomPlatform.Application.DTOs.Domains
{
    public class CreateTenantDomainDto
    {
        public string Domain { get; set; } = string.Empty;
        public bool IsPrimary { get; set; } = false;
        public Guid TenantId { get; set; }
    }
}