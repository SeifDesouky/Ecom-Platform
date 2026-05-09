using EcomPlatform.Core.Entities.Common;

namespace EcomPlatform.Core.Entities
{
    public class PasswordResetToken : BaseEntity
    {
        public Guid UserId { get; set; }
        public User? User { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; } = false;
    }
}