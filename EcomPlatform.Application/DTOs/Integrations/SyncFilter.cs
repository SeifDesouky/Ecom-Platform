namespace EcomPlatform.Application.DTOs.Integrations
{
    public class SyncFilter
    {
        /// <summary>جيب البيانات من تاريخ معين</summary>
        public DateTime? ModifiedAfter { get; init; }

        /// <summary>رقم الصفحة — للـ APIs اللي بتستخدم page-based pagination</summary>
        public int Page { get; init; } = 1;

        /// <summary>حجم الصفحة</summary>
        public int PageSize { get; init; } = 50;

        /// <summary>External IDs محددة تتجاب بس</summary>
        public IReadOnlyList<string>? ExternalIds { get; init; }

        /// <summary>
        /// Cursor-based pagination للـ APIs اللي بتدعمها زي Shopify و Salla
        /// Shopify بترجع page_info في الـ Link header — اتبعت منه
        /// </summary>
        public string? Cursor { get; init; }
    }
}