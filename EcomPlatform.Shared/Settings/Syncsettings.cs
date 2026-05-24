namespace EcomPlatform.Shared.Settings
{
    public sealed class SyncSettings
    {
        /// <summary>كل قد إيه (بالدقايق) يعمل BackgroundSyncJob دورة sync</summary>
        public int IntervalMinutes { get; init; } = 30;
    }
}