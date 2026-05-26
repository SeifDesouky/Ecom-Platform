using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Profile
{
    /// <summary>
    /// الـ Admin بيقدر يغير كل حاجة + الـ Role والـ IsActive
    /// </summary>
    public class AdminUpdateProfileDto : UpdateProfileDto
    {
        public string? Email { get; set; }
        public UserRole? Role { get; set; }
        public bool? IsActive { get; set; }
    }
}