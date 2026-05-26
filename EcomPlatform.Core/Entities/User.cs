// ================================================================
// EcomPlatform.Core/Entities/User.cs
// ================================================================
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

        // ✅ nullable — مش كل يوزر عنده password (Social-only accounts)
        public string? PasswordHash { get; set; }

        public UserRole Role { get; set; } = UserRole.Customer;
        public bool IsActive { get; set; } = true;
        public bool IsEmailVerified { get; set; } = false;
        public DateTime? LastLoginAt { get; set; }

        // ✅ جديد: Social Login IDs
        public string? GoogleId { get; set; }
        public string? AppleId { get; set; }

        // ── Multi-Tenant ─────────────────────────────────────────────────
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        // ── Navigation ───────────────────────────────────────────────────
        public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
        public UserProfile? Profile { get; set; }
    }
}