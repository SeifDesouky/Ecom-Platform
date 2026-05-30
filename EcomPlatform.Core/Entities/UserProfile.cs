using EcomPlatform.Core.Entities.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcomPlatform.Core.Entities
{
    [Table("UserProfiles")]
    public class UserProfile : BaseEntity
    {
        // ── FK ───────────────────────────────────────────────────────────
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        // ── Profile Fields ───────────────────────────────────────────────
        public string? AvatarUrl { get; set; }
        public string? Bio { get; set; }
        public DateTime? DateOfBirth { get; set; }

        // ── Address ──────────────────────────────────────────────────────
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
    }
}