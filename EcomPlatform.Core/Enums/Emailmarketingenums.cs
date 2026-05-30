namespace EcomPlatform.Core.Enums
{
    public enum CampaignStatus
    {
        Draft = 1,   // مسودة — لسه بيتعدل
        Scheduled = 2,   // مجدول لوقت معين
        Sending = 3,   // بيتبعت دلوقتي
        Sent = 4,   // اتبعت خلاص
        Cancelled = 5,   // اتلغى قبل الإرسال
        Failed = 6    // فشل الإرسال
    }

    public enum SubscriberStatus
    {
        Active = 1,
        Unsubscribed = 2,
        Bounced = 3,  // الإيميل مش شغال
        Complained = 4   // بلّغ عن الإيميل كـ Spam
    }

    public enum CampaignRecipientStatus
    {
        Pending = 1,
        Sent = 2,
        Delivered = 3,
        Opened = 4,
        Clicked = 5,
        Bounced = 6,
        Complained = 7
    }
}