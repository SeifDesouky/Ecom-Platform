namespace EcomPlatform.Application.DTOs.Customers
{
    public class CustomerResponseDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName => $"{FirstName} {LastName}";
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public DateTime? BirthDate { get; set; }
        public bool IsActive { get; set; }
        public bool IsEmailVerified { get; set; }
        public string Notes { get; set; } = string.Empty;
        public decimal TotalSpent { get; set; }
        public int TotalOrders { get; set; }
        public Guid TenantId { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<CustomerAddressResponseDto> Addresses { get; set; } = new();
    }

    public class CustomerAddressResponseDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }
}