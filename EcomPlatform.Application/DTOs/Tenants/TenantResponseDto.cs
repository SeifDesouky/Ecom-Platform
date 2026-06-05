using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Tenants
{
    public class TenantResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Logo { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public TenantStatus Status { get; set; }
        public DateTime? SubscriptionEndDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public int UsersCount { get; set; }
        public string? VatNumber { get; set; }
        public decimal VatRate { get; set; }
        public string StoreStatus { get; set; } = string.Empty;
    }
}
