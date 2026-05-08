// ================================================================
// EcomPlatform.Application/DTOs/Auth/RefreshTokenDtos.cs
// ================================================================
namespace EcomPlatform.Application.DTOs.Auth
{
    /// <summary>
    /// الـ request لتجديد الـ Access Token
    /// </summary>
    public class RefreshTokenRequestDto
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    /// <summary>
    /// الـ request لعمل Revoke لـ token واحد (logout من device معين)
    /// </summary>
    public class RevokeTokenRequestDto
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    /// <summary>
    /// معلومات device/session للعرض للـ user
    /// </summary>
    public class ActiveSessionDto
    {
        public Guid TokenId { get; set; }
        public string? DeviceInfo { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsCurrentSession { get; set; }
    }
}
