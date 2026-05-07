using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Users
{
    public class UpdateUserDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public UserRole Role { get; set; }
    }
}