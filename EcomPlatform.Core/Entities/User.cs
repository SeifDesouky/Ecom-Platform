using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    public class User : BaseEntity
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Customer;
        public bool IsActive { get; set; } = true;
        public bool IsEmailVerified { get; set; } = false;
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiry { get; set; }
        public DateTime? LastLoginAt { get; set; }

        // Multi-Tenant
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
    }
}