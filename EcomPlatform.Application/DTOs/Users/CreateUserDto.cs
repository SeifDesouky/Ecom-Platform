using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Users
{
    public class CreateUserDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.TenantStaff;
        public Guid? TenantId { get; set; }
    }
}