using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Domains
{
    public class TenantDomainResponseDto
    {
        public Guid Id { get; set; }
        public string Domain { get; set; } = string.Empty;
        public DomainStatus Status { get; set; }
        public bool IsPrimary { get; set; }
        public bool SSLEnabled { get; set; }
        public DateTime? SSLExpiryDate { get; set; }
        public string VerificationToken { get; set; } = string.Empty;
        public DateTime? VerifiedAt { get; set; }
        public string Notes { get; set; } = string.Empty;
        public Guid TenantId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}