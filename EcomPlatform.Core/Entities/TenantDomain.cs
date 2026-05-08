using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    public class TenantDomain : BaseEntity, ITenantEntity
    {
        public string Domain { get; set; } = string.Empty;
        public DomainStatus Status { get; set; } = DomainStatus.Pending;
        public bool IsPrimary { get; set; } = false;
        public bool SSLEnabled { get; set; } = false;
        public DateTime? SSLExpiryDate { get; set; }
        public string VerificationToken { get; set; } = string.Empty;
        public DateTime? VerifiedAt { get; set; }
        public string Notes { get; set; } = string.Empty;

        // Relations
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
    }
}