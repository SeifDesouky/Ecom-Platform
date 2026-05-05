namespace EcomPlatform.Application.DTOs.Plans
{
    public class CreatePlanDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal MonthlyPrice { get; set; }
        public decimal YearlyPrice { get; set; }
        public bool IsPopular { get; set; } = false;
        public int MaxProducts { get; set; }
        public int MaxOrders { get; set; }
        public int MaxCustomers { get; set; }
        public int MaxUsers { get; set; }
        public bool HasAnalytics { get; set; } = false;
        public bool HasAPI { get; set; } = false;
        public bool HasMultiCurrency { get; set; } = false;
        public bool HasCustomDomain { get; set; } = false;
        public bool HasPrioritySupport { get; set; } = false;
    }
}