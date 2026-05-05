namespace EcomPlatform.Application.DTOs.Customers
{
    public class CreateCustomerDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public DateTime? BirthDate { get; set; }
        public string Notes { get; set; } = string.Empty;
        public Guid TenantId { get; set; }
    }
}