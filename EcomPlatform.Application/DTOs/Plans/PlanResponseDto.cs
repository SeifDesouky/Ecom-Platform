namespace EcomPlatform.Application.DTOs.Plans
{
    public class PlanResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal MonthlyPrice { get; set; }
        public decimal YearlyPrice { get; set; }
        public bool IsActive { get; set; }
        public bool IsPopular { get; set; }
        public int MaxProducts { get; set; }
        public int MaxOrders { get; set; }
        public int MaxCustomers { get; set; }
        public int MaxUsers { get; set; }
        public bool HasAnalytics { get; set; }
        public bool HasAPI { get; set; }
        public bool HasMultiCurrency { get; set; }
        public bool HasCustomDomain { get; set; }
        public bool HasPrioritySupport { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}