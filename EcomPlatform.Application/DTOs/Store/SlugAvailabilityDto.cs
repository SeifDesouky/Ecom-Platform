// ================================================================
// EcomPlatform.Application/DTOs/Store/SlugAvailabilityDto.cs
// ================================================================
namespace EcomPlatform.Application.DTOs.Store
{
    public class SlugAvailabilityResponseDto
    {
        public string Slug { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public string? Message { get; set; }
    }
}