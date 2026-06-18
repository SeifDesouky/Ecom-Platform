// ================================================================
// EcomPlatform.Application/DTOs/Auth/OnboardStoreDto.cs
// ================================================================
namespace EcomPlatform.Application.DTOs.Auth
{
    public class OnboardStoreDto
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Logo { get; set; }
        public string? Domain { get; set; }
        public string? VatNumber { get; set; }
        public decimal VatRate { get; set; } = 0.15m;
    }
}