using System.ComponentModel.DataAnnotations;

namespace EcomPlatform.Application.DTOs.Inventory
{
    // ── Request DTOs ──────────────────────────────────────────────────────────
    public class CreateWarehouseDto
    {
        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string ManagerName { get; set; } = string.Empty;
        public bool IsDefault { get; set; } = false;
        public Guid TenantId { get; set; }
    }

    public class UpdateWarehouseDto
    {
        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string ManagerName { get; set; } = string.Empty;
    }

    // ── Response DTOs ─────────────────────────────────────────────────────────
    public class WarehouseResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string ManagerName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
        public Guid? TenantId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
