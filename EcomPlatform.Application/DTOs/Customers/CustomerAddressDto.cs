namespace EcomPlatform.Application.DTOs.Customers
{
    public class CreateCustomerAddressDto
    {
        public string Title { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public bool IsDefault { get; set; } = false;
        public Guid CustomerId { get; set; }
    }
}