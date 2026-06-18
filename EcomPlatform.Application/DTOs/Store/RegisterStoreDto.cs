// ================================================================
// EcomPlatform.Application/DTOs/Store/RegisterStoreDto.cs
// ================================================================
using System.ComponentModel.DataAnnotations;

namespace EcomPlatform.Application.DTOs.Store
{
    public class RegisterStoreDto
    {
        // ── بيانات المتجر ────────────────────────────────────────────────
        [Required]
        [MaxLength(100)]
        public string StoreName { get; set; } = string.Empty;

        /// <summary>
        /// الـ URL الفرعي للمتجر — حروف صغيرة وأرقام وشرطة فقط
        /// </summary>
        [Required]
        [MaxLength(60)]
        [RegularExpression(@"^[a-z0-9]+(?:-[a-z0-9]+)*$",
            ErrorMessage = "Slug must be lowercase letters, numbers, and hyphens only")]
        public string Slug { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? Logo { get; set; }

        [MaxLength(7)]
        [RegularExpression(@"^#[0-9A-Fa-f]{6}$",
            ErrorMessage = "ThemeColor must be a valid hex color e.g. #10B981")]
        public string ThemeColor { get; set; } = "#10B981";

        [MaxLength(100)]
        public string? Domain { get; set; }

        // ── بيانات الأدمن الأول ──────────────────────────────────────────
        [Required]
        [EmailAddress]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [MaxLength(60)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(60)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Phone { get; set; }
    }
}