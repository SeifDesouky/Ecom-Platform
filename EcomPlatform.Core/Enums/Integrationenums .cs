namespace EcomPlatform.Core.Enums
{
    public enum SyncStatus
    {
        Pending = 0,
        InProgress = 1,
        Success = 2,
        Failed = 3,
        PartialSuccess = 4,
    }

    public enum SyncDirection
    {
        Import = 1,   // من المنصة إلى Fatora
        Export = 2,   // من Fatora إلى المنصة
        BiDirectional = 3,
    }

    public enum SyncEntityType
    {
        Products = 1,
        Orders = 2,
        Customers = 3,
        Inventory = 4,
        Categories = 5,
        Prices = 6,
    }

    public enum WebhookEventStatus
    {
        Received = 0,
        Processing = 1,
        Processed = 2,
        Failed = 3,
        Ignored = 4,
    }

    public enum IntegrationStatus
    {
        Active = 1,
        Inactive = 2,
        Error = 3,
        PendingSetup = 4,
    }
}