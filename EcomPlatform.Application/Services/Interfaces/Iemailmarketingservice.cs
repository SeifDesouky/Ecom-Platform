using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.EmailMarketing;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IEmailMarketingService
    {
        // ── Mailing Lists ─────────────────────────────────────────────────────

        Task<ApiResponse<MailingListResponseDto>> CreateListAsync(CreateMailingListDto dto);
        Task<ApiResponse<MailingListResponseDto>> UpdateListAsync(Guid id, UpdateMailingListDto dto);
        Task<ApiResponse<bool>> DeleteListAsync(Guid id);
        Task<ApiResponse<MailingListResponseDto>> GetListByIdAsync(Guid id);
        Task<ApiResponse<PagedResponse<MailingListResponseDto>>> GetListsAsync(
            Guid tenantId, PaginationParams pagination);

        // ── Subscribers ───────────────────────────────────────────────────────

        /// <summary>إضافة مشترك واحد — يرسل Welcome Email تلقائياً لو مضبوط</summary>
        Task<ApiResponse<SubscriberResponseDto>> AddSubscriberAsync(AddSubscriberDto dto);

        /// <summary>استيراد بالجملة مع تخطي المكررين</summary>
        Task<ApiResponse<ImportResultDto>> ImportSubscribersAsync(ImportSubscribersDto dto);

        /// <summary>إلغاء الاشتراك عبر Token (من رابط Unsubscribe في الإيميل)</summary>
        Task<ApiResponse<bool>> UnsubscribeByTokenAsync(string token);

        /// <summary>إلغاء الاشتراك من الداشبورد (بـ ID)</summary>
        Task<ApiResponse<bool>> UnsubscribeAsync(Guid subscriberId);

        Task<ApiResponse<PagedResponse<SubscriberResponseDto>>> GetSubscribersAsync(
            Guid mailingListId, PaginationParams pagination);

        Task<ApiResponse<bool>> DeleteSubscriberAsync(Guid subscriberId);

        // ── Campaigns ─────────────────────────────────────────────────────────

        Task<ApiResponse<CampaignResponseDto>> CreateCampaignAsync(CreateCampaignDto dto);
        Task<ApiResponse<CampaignResponseDto>> UpdateCampaignAsync(Guid id, UpdateCampaignDto dto);
        Task<ApiResponse<bool>> DeleteCampaignAsync(Guid id);
        Task<ApiResponse<CampaignResponseDto>> GetCampaignByIdAsync(Guid id);
        Task<ApiResponse<PagedResponse<CampaignResponseDto>>> GetCampaignsAsync(
            Guid tenantId, PaginationParams pagination);

        /// <summary>إرسال الحملة فوراً أو جدولتها</summary>
        Task<ApiResponse<CampaignResponseDto>> SendCampaignAsync(Guid campaignId);

        /// <summary>إلغاء حملة مجدولة</summary>
        Task<ApiResponse<bool>> CancelCampaignAsync(Guid campaignId);

        // ── Tracking ──────────────────────────────────────────────────────────

        /// <summary>تسجيل حدث فتح الإيميل (Tracking Pixel)</summary>
        Task TrackOpenAsync(string trackingToken);

        /// <summary>تسجيل حدث النقر على رابط</summary>
        Task TrackClickAsync(string trackingToken, string url);
    }
}