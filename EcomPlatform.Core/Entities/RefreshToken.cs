// ================================================================
// EcomPlatform.Core/Entities/RefreshToken.cs
// ================================================================
using EcomPlatform.Core.Entities.Common;

namespace EcomPlatform.Core.Entities
{
    /// <summary>
    /// جدول منفصل للـ Refresh Tokens بدل تخزينهم على User مباشرة.
    /// كل token له record مستقل — بيسمح بـ:
    ///   - Device tracking
    ///   - Logout from all devices
    ///   - Reuse detection (rotation)
    ///   - Hashed storage
    /// </summary>
    public class RefreshToken : BaseEntity
    {
        // ── الـ Token المخزن كـ SHA-256 Hash (مش plain text) ──────────────
        public string TokenHash { get; set; } = string.Empty;

        // ── FK للـ User ───────────────────────────────────────────────────
        public Guid UserId { get; set; }
        public User? User { get; set; }

        // ── Expiry ────────────────────────────────────────────────────────
        public DateTime ExpiresAt { get; set; }

        // ── Device / Client Tracking ──────────────────────────────────────
        public string? DeviceInfo { get; set; }   // User-Agent
        public string? IpAddress { get; set; }

        // ── Lifecycle ────────────────────────────────────────────────────
        public bool IsRevoked { get; set; } = false;
        public DateTime? RevokedAt { get; set; }

        // ── Rotation Chain ────────────────────────────────────────────────
        // لو الـ token اتاستخدم وتم rotate، بنحفظ hash الـ token الجديد هنا
        // بيساعد في Reuse Detection — لو حد بعث token قديم محلوش
        public string? ReplacedByTokenHash { get; set; }

        // ── Computed ─────────────────────────────────────────────────────
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool IsActive => !IsRevoked && !IsExpired;
    }
}
