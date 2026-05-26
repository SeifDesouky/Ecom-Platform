CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) NOT NULL,
    `ProductVersion` varchar(32) NOT NULL,
    PRIMARY KEY (`MigrationId`)
);

START TRANSACTION;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `Plans` (
        `Id` char(36) NOT NULL,
        `Name` varchar(100) NOT NULL,
        `Description` varchar(500) NOT NULL,
        `MonthlyPrice` decimal(18,2) NOT NULL,
        `YearlyPrice` decimal(18,2) NOT NULL,
        `IsActive` tinyint(1) NOT NULL,
        `IsPopular` tinyint(1) NOT NULL,
        `MaxProducts` int NOT NULL,
        `MaxOrders` int NOT NULL,
        `MaxCustomers` int NOT NULL,
        `MaxUsers` int NOT NULL,
        `HasAnalytics` tinyint(1) NOT NULL,
        `HasAPI` tinyint(1) NOT NULL,
        `HasMultiCurrency` tinyint(1) NOT NULL,
        `HasCustomDomain` tinyint(1) NOT NULL,
        `HasPrioritySupport` tinyint(1) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`)
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `Tenants` (
        `Id` char(36) NOT NULL,
        `Name` varchar(100) NOT NULL,
        `Slug` varchar(100) NOT NULL,
        `Email` varchar(150) NOT NULL,
        `Phone` longtext NOT NULL,
        `Logo` longtext NOT NULL,
        `Domain` varchar(200) NOT NULL,
        `IsActive` tinyint(1) NOT NULL,
        `Status` int NOT NULL,
        `SubscriptionEndDate` datetime(6) NULL,
        `VatNumber` longtext NULL,
        `VatRate` decimal(5,2) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`)
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `Categories` (
        `Id` char(36) NOT NULL,
        `Name` varchar(100) NOT NULL,
        `Slug` varchar(100) NOT NULL,
        `Description` longtext NOT NULL,
        `Image` longtext NOT NULL,
        `IsActive` tinyint(1) NOT NULL,
        `ParentId` char(36) NULL,
        `TenantId` char(36) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_Categories_Categories_ParentId` FOREIGN KEY (`ParentId`) REFERENCES `Categories` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_Categories_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `Coupons` (
        `Id` char(36) NOT NULL,
        `Code` varchar(50) NOT NULL,
        `Description` varchar(200) NOT NULL,
        `Type` int NOT NULL,
        `Value` decimal(18,2) NOT NULL,
        `MinOrderAmount` decimal(18,2) NULL,
        `MaxDiscountAmount` decimal(18,2) NULL,
        `UsageLimit` int NULL,
        `UsageCount` int NOT NULL,
        `IsActive` tinyint(1) NOT NULL,
        `StartDate` datetime(6) NULL,
        `EndDate` datetime(6) NULL,
        `TenantId` char(36) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_Coupons_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `Customers` (
        `Id` char(36) NOT NULL,
        `FirstName` varchar(50) NOT NULL,
        `LastName` varchar(50) NOT NULL,
        `Email` varchar(150) NOT NULL,
        `Phone` varchar(20) NOT NULL,
        `Avatar` longtext NOT NULL,
        `BirthDate` datetime(6) NULL,
        `IsActive` tinyint(1) NOT NULL,
        `IsEmailVerified` tinyint(1) NOT NULL,
        `Notes` longtext NOT NULL,
        `TotalSpent` decimal(18,2) NOT NULL,
        `TotalOrders` int NOT NULL,
        `TenantId` char(36) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_Customers_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `DashboardSnapshots` (
        `Id` char(36) NOT NULL,
        `TenantId` char(36) NULL,
        `TotalRevenue` decimal(18,2) NOT NULL,
        `RevenueThisMonth` decimal(18,2) NOT NULL,
        `TotalOrders` int NOT NULL,
        `OrdersThisMonth` int NOT NULL,
        `TotalCustomers` int NOT NULL,
        `NewCustomersThisMonth` int NOT NULL,
        `TotalProducts` int NOT NULL,
        `ActiveProducts` int NOT NULL,
        `LowStockProducts` int NOT NULL,
        `PendingOrders` int NOT NULL,
        `ProcessingOrders` int NOT NULL,
        `ShippedOrders` int NOT NULL,
        `DeliveredOrders` int NOT NULL,
        `CancelledOrders` int NOT NULL,
        `SnapshotDate` datetime(6) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_DashboardSnapshots_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `Pages` (
        `Id` char(36) NOT NULL,
        `Title` varchar(200) NOT NULL,
        `Slug` varchar(200) NOT NULL,
        `Content` longtext NOT NULL,
        `MetaTitle` varchar(200) NOT NULL,
        `MetaDescription` varchar(500) NOT NULL,
        `IsPublished` tinyint(1) NOT NULL,
        `Type` int NOT NULL,
        `SortOrder` int NOT NULL,
        `TenantId` char(36) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_Pages_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `Settings` (
        `Id` char(36) NOT NULL,
        `Key` varchar(100) NOT NULL,
        `Value` varchar(2000) NOT NULL,
        `Group` varchar(50) NOT NULL,
        `Description` varchar(300) NOT NULL,
        `IsPublic` tinyint(1) NOT NULL,
        `TenantId` char(36) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_Settings_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `ShippingZones` (
        `Id` char(36) NOT NULL,
        `Name` varchar(100) NOT NULL,
        `Description` varchar(300) NOT NULL,
        `IsActive` tinyint(1) NOT NULL,
        `TenantId` char(36) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_ShippingZones_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `Subscriptions` (
        `Id` char(36) NOT NULL,
        `Status` int NOT NULL,
        `Period` int NOT NULL,
        `Price` decimal(18,2) NOT NULL,
        `StartDate` datetime(6) NOT NULL,
        `EndDate` datetime(6) NOT NULL,
        `AutoRenew` tinyint(1) NOT NULL,
        `CancelledAt` datetime(6) NULL,
        `Notes` longtext NOT NULL,
        `TenantId` char(36) NULL,
        `PlanId` char(36) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_Subscriptions_Plans_PlanId` FOREIGN KEY (`PlanId`) REFERENCES `Plans` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_Subscriptions_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `TenantDomains` (
        `Id` char(36) NOT NULL,
        `Domain` varchar(200) NOT NULL,
        `Status` int NOT NULL,
        `IsPrimary` tinyint(1) NOT NULL,
        `SSLEnabled` tinyint(1) NOT NULL,
        `SSLExpiryDate` datetime(6) NULL,
        `VerificationToken` varchar(100) NOT NULL,
        `VerifiedAt` datetime(6) NULL,
        `Notes` varchar(500) NOT NULL,
        `TenantId` char(36) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_TenantDomains_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `Users` (
        `Id` char(36) NOT NULL,
        `FirstName` varchar(50) NOT NULL,
        `LastName` varchar(50) NOT NULL,
        `Email` varchar(150) NOT NULL,
        `Phone` longtext NOT NULL,
        `PasswordHash` longtext NOT NULL,
        `Role` int NOT NULL,
        `IsActive` tinyint(1) NOT NULL,
        `IsEmailVerified` tinyint(1) NOT NULL,
        `LastLoginAt` datetime(6) NULL,
        `TenantId` char(36) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_Users_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `Products` (
        `Id` char(36) NOT NULL,
        `Name` varchar(200) NOT NULL,
        `Slug` varchar(200) NOT NULL,
        `Description` longtext NOT NULL,
        `ShortDescription` longtext NOT NULL,
        `Price` decimal(18,2) NOT NULL,
        `ComparePrice` decimal(18,2) NULL,
        `CostPrice` decimal(18,2) NULL,
        `SKU` varchar(100) NOT NULL,
        `Barcode` longtext NOT NULL,
        `Stock` int NOT NULL,
        `LowStockAlert` int NOT NULL,
        `TrackInventory` tinyint(1) NOT NULL,
        `IsActive` tinyint(1) NOT NULL,
        `IsFeatured` tinyint(1) NOT NULL,
        `Status` int NOT NULL,
        `MetaTitle` longtext NOT NULL,
        `MetaDescription` longtext NOT NULL,
        `Weight` decimal(18,2) NOT NULL,
        `TenantId` char(36) NULL,
        `CategoryId` char(36) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_Products_Categories_CategoryId` FOREIGN KEY (`CategoryId`) REFERENCES `Categories` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_Products_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `CustomerAddresses` (
        `Id` char(36) NOT NULL,
        `Title` varchar(50) NOT NULL,
        `FullName` varchar(100) NOT NULL,
        `Phone` varchar(20) NOT NULL,
        `Address` varchar(300) NOT NULL,
        `City` varchar(100) NOT NULL,
        `Country` varchar(100) NOT NULL,
        `PostalCode` varchar(20) NOT NULL,
        `IsDefault` tinyint(1) NOT NULL,
        `CustomerId` char(36) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_CustomerAddresses_Customers_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `Customers` (`Id`) ON DELETE CASCADE
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `ShippingMethods` (
        `Id` char(36) NOT NULL,
        `Name` varchar(100) NOT NULL,
        `Description` longtext NOT NULL,
        `Type` int NOT NULL,
        `Cost` decimal(18,2) NOT NULL,
        `MinOrderAmount` decimal(18,2) NULL,
        `MaxOrderAmount` decimal(18,2) NULL,
        `EstimatedDaysMin` int NULL,
        `EstimatedDaysMax` int NULL,
        `IsActive` tinyint(1) NOT NULL,
        `ShippingZoneId` char(36) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_ShippingMethods_ShippingZones_ShippingZoneId` FOREIGN KEY (`ShippingZoneId`) REFERENCES `ShippingZones` (`Id`) ON DELETE CASCADE
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `Articles` (
        `Id` char(36) NOT NULL,
        `Title` varchar(200) NOT NULL,
        `Slug` varchar(200) NOT NULL,
        `Content` longtext NOT NULL,
        `Excerpt` varchar(500) NOT NULL,
        `CoverImage` varchar(500) NOT NULL,
        `MetaTitle` longtext NOT NULL,
        `MetaDescription` longtext NOT NULL,
        `IsPublished` tinyint(1) NOT NULL,
        `PublishedAt` datetime(6) NULL,
        `Tags` varchar(500) NOT NULL,
        `ViewCount` int NOT NULL,
        `TenantId` char(36) NULL,
        `AuthorId` char(36) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_Articles_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_Articles_Users_AuthorId` FOREIGN KEY (`AuthorId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `AuditLogs` (
        `Id` char(36) NOT NULL,
        `EntityName` varchar(100) NOT NULL,
        `EntityId` varchar(50) NOT NULL,
        `Action` int NOT NULL,
        `OldValue` longtext NOT NULL,
        `NewValue` longtext NOT NULL,
        `IPAddress` varchar(50) NOT NULL,
        `UserAgent` varchar(300) NOT NULL,
        `UserId` char(36) NOT NULL,
        `TenantId` char(36) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_AuditLogs_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_AuditLogs_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `Notifications` (
        `Id` char(36) NOT NULL,
        `Title` varchar(200) NOT NULL,
        `Message` varchar(500) NOT NULL,
        `Type` int NOT NULL,
        `IsRead` tinyint(1) NOT NULL,
        `ReadAt` datetime(6) NULL,
        `ActionUrl` varchar(300) NULL,
        `Icon` varchar(100) NULL,
        `UserId` char(36) NOT NULL,
        `TenantId` char(36) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_Notifications_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_Notifications_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `Orders` (
        `Id` char(36) NOT NULL,
        `OrderNumber` varchar(50) NOT NULL,
        `Status` int NOT NULL,
        `PaymentStatus` int NOT NULL,
        `SubTotal` decimal(18,2) NOT NULL,
        `ShippingCost` decimal(18,2) NOT NULL,
        `Discount` decimal(18,2) NOT NULL,
        `Tax` decimal(18,2) NOT NULL,
        `Total` decimal(18,2) NOT NULL,
        `Notes` longtext NOT NULL,
        `ShippingAddress` longtext NOT NULL,
        `ShippingCity` varchar(100) NOT NULL,
        `ShippingCountry` varchar(100) NOT NULL,
        `ShippingPhone` longtext NOT NULL,
        `CustomerName` varchar(100) NOT NULL,
        `CustomerEmail` varchar(150) NOT NULL,
        `CustomerPhone` varchar(20) NOT NULL,
        `PaidAt` datetime(6) NULL,
        `ShippedAt` datetime(6) NULL,
        `DeliveredAt` datetime(6) NULL,
        `TenantId` char(36) NULL,
        `CustomerId` char(36) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_Orders_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_Orders_Users_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `PasswordResetTokens` (
        `Id` char(36) NOT NULL,
        `UserId` char(36) NOT NULL,
        `Token` longtext NOT NULL,
        `ExpiresAt` datetime(6) NOT NULL,
        `IsUsed` tinyint(1) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_PasswordResetTokens_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `RefreshTokens` (
        `Id` char(36) NOT NULL,
        `TokenHash` varchar(128) NOT NULL,
        `UserId` char(36) NOT NULL,
        `ExpiresAt` datetime(6) NOT NULL,
        `DeviceInfo` varchar(512) NULL,
        `IpAddress` varchar(45) NULL,
        `IsRevoked` tinyint(1) NOT NULL,
        `RevokedAt` datetime(6) NULL,
        `ReplacedByTokenHash` varchar(128) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_RefreshTokens_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `Tickets` (
        `Id` char(36) NOT NULL,
        `Subject` varchar(200) NOT NULL,
        `Message` longtext NOT NULL,
        `Status` int NOT NULL,
        `Priority` int NOT NULL,
        `Category` varchar(100) NOT NULL,
        `TenantId` char(36) NULL,
        `CreatedById` char(36) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_Tickets_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_Tickets_Users_CreatedById` FOREIGN KEY (`CreatedById`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `ProductImages` (
        `Id` char(36) NOT NULL,
        `Url` varchar(500) NOT NULL,
        `Alt` varchar(200) NOT NULL,
        `SortOrder` int NOT NULL,
        `IsMain` tinyint(1) NOT NULL,
        `ProductId` char(36) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_ProductImages_Products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE CASCADE
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `Invoices` (
        `Id` char(36) NOT NULL,
        `InvoiceNumber` varchar(50) NOT NULL,
        `Status` int NOT NULL,
        `SubTotal` decimal(18,2) NOT NULL,
        `Tax` decimal(18,2) NOT NULL,
        `Discount` decimal(18,2) NOT NULL,
        `Total` decimal(18,2) NOT NULL,
        `Notes` longtext NOT NULL,
        `PaidAt` datetime(6) NULL,
        `DueDate` datetime(6) NOT NULL,
        `CustomerName` varchar(100) NOT NULL,
        `CustomerEmail` varchar(150) NOT NULL,
        `CustomerPhone` varchar(20) NOT NULL,
        `CustomerAddress` longtext NOT NULL,
        `QrCodeBase64` longtext NOT NULL,
        `ZatcaXml` longtext NOT NULL,
        `TenantId` char(36) NULL,
        `OrderId` char(36) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_Invoices_Orders_OrderId` FOREIGN KEY (`OrderId`) REFERENCES `Orders` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_Invoices_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `OrderItems` (
        `Id` char(36) NOT NULL,
        `Quantity` int NOT NULL,
        `UnitPrice` decimal(18,2) NOT NULL,
        `TotalPrice` decimal(18,2) NOT NULL,
        `ProductName` varchar(200) NOT NULL,
        `ProductSKU` varchar(100) NOT NULL,
        `ProductImage` varchar(500) NOT NULL,
        `OrderId` char(36) NOT NULL,
        `ProductId` char(36) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_OrderItems_Orders_OrderId` FOREIGN KEY (`OrderId`) REFERENCES `Orders` (`Id`) ON DELETE CASCADE,
        CONSTRAINT `FK_OrderItems_Products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `TicketReplies` (
        `Id` char(36) NOT NULL,
        `Message` longtext NOT NULL,
        `IsStaff` tinyint(1) NOT NULL,
        `TicketId` char(36) NOT NULL,
        `CreatedById` char(36) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_TicketReplies_Tickets_TicketId` FOREIGN KEY (`TicketId`) REFERENCES `Tickets` (`Id`) ON DELETE CASCADE,
        CONSTRAINT `FK_TicketReplies_Users_CreatedById` FOREIGN KEY (`CreatedById`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE TABLE `InvoiceItems` (
        `Id` char(36) NOT NULL,
        `Description` varchar(300) NOT NULL,
        `Quantity` int NOT NULL,
        `UnitPrice` decimal(18,2) NOT NULL,
        `TotalPrice` decimal(18,2) NOT NULL,
        `InvoiceId` char(36) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_InvoiceItems_Invoices_InvoiceId` FOREIGN KEY (`InvoiceId`) REFERENCES `Invoices` (`Id`) ON DELETE CASCADE
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Articles_AuthorId` ON `Articles` (`AuthorId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE UNIQUE INDEX `IX_Articles_Slug_TenantId` ON `Articles` (`Slug`, `TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Articles_TenantId` ON `Articles` (`TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_AuditLogs_TenantId` ON `AuditLogs` (`TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_AuditLogs_TenantId_CreatedAt` ON `AuditLogs` (`TenantId`, `CreatedAt`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_AuditLogs_TenantId_EntityName` ON `AuditLogs` (`TenantId`, `EntityName`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_AuditLogs_TenantId_UserId` ON `AuditLogs` (`TenantId`, `UserId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_AuditLogs_UserId` ON `AuditLogs` (`UserId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Categories_ParentId` ON `Categories` (`ParentId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE UNIQUE INDEX `IX_Categories_Slug_TenantId` ON `Categories` (`Slug`, `TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Categories_TenantId` ON `Categories` (`TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Categories_TenantId_ParentId` ON `Categories` (`TenantId`, `ParentId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE UNIQUE INDEX `IX_Coupons_Code_TenantId` ON `Coupons` (`Code`, `TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Coupons_TenantId` ON `Coupons` (`TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Coupons_TenantId_IsActive` ON `Coupons` (`TenantId`, `IsActive`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_CustomerAddresses_CustomerId` ON `CustomerAddresses` (`CustomerId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE UNIQUE INDEX `IX_Customers_Email_TenantId` ON `Customers` (`Email`, `TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Customers_TenantId` ON `Customers` (`TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Customers_TenantId_CreatedAt` ON `Customers` (`TenantId`, `CreatedAt`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_DashboardSnapshots_TenantId` ON `DashboardSnapshots` (`TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_InvoiceItems_InvoiceId` ON `InvoiceItems` (`InvoiceId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE UNIQUE INDEX `IX_Invoices_InvoiceNumber` ON `Invoices` (`InvoiceNumber`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Invoices_OrderId` ON `Invoices` (`OrderId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Invoices_TenantId` ON `Invoices` (`TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Invoices_TenantId_CreatedAt` ON `Invoices` (`TenantId`, `CreatedAt`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Invoices_TenantId_Status` ON `Invoices` (`TenantId`, `Status`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Notifications_TenantId` ON `Notifications` (`TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Notifications_TenantId_IsRead` ON `Notifications` (`TenantId`, `IsRead`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Notifications_TenantId_UserId` ON `Notifications` (`TenantId`, `UserId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Notifications_UserId` ON `Notifications` (`UserId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_OrderItems_OrderId` ON `OrderItems` (`OrderId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_OrderItems_ProductId` ON `OrderItems` (`ProductId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Orders_CustomerId` ON `Orders` (`CustomerId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE UNIQUE INDEX `IX_Orders_OrderNumber` ON `Orders` (`OrderNumber`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Orders_TenantId` ON `Orders` (`TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Orders_TenantId_CreatedAt` ON `Orders` (`TenantId`, `CreatedAt`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Orders_TenantId_Status` ON `Orders` (`TenantId`, `Status`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE UNIQUE INDEX `IX_Pages_Slug_TenantId` ON `Pages` (`Slug`, `TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Pages_TenantId` ON `Pages` (`TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_PasswordResetTokens_UserId` ON `PasswordResetTokens` (`UserId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_ProductImages_ProductId` ON `ProductImages` (`ProductId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Products_CategoryId` ON `Products` (`CategoryId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE UNIQUE INDEX `IX_Products_Slug_TenantId` ON `Products` (`Slug`, `TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Products_TenantId` ON `Products` (`TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Products_TenantId_CategoryId` ON `Products` (`TenantId`, `CategoryId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Products_TenantId_IsActive` ON `Products` (`TenantId`, `IsActive`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Products_TenantId_Status` ON `Products` (`TenantId`, `Status`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE UNIQUE INDEX `IX_RefreshTokens_TokenHash` ON `RefreshTokens` (`TokenHash`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_RefreshTokens_UserId` ON `RefreshTokens` (`UserId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_RefreshTokens_UserId_IsRevoked_ExpiresAt` ON `RefreshTokens` (`UserId`, `IsRevoked`, `ExpiresAt`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE UNIQUE INDEX `IX_Settings_Key_TenantId` ON `Settings` (`Key`, `TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Settings_TenantId` ON `Settings` (`TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_ShippingMethods_ShippingZoneId` ON `ShippingMethods` (`ShippingZoneId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_ShippingZones_TenantId` ON `ShippingZones` (`TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Subscriptions_PlanId` ON `Subscriptions` (`PlanId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Subscriptions_TenantId` ON `Subscriptions` (`TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE UNIQUE INDEX `IX_TenantDomains_Domain` ON `TenantDomains` (`Domain`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_TenantDomains_TenantId` ON `TenantDomains` (`TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE UNIQUE INDEX `IX_Tenants_Email` ON `Tenants` (`Email`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE UNIQUE INDEX `IX_Tenants_Slug` ON `Tenants` (`Slug`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_TicketReplies_CreatedById` ON `TicketReplies` (`CreatedById`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_TicketReplies_TicketId` ON `TicketReplies` (`TicketId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Tickets_CreatedById` ON `Tickets` (`CreatedById`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Tickets_TenantId` ON `Tickets` (`TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Tickets_TenantId_CreatedAt` ON `Tickets` (`TenantId`, `CreatedAt`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Tickets_TenantId_Status` ON `Tickets` (`TenantId`, `Status`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE UNIQUE INDEX `IX_Users_Email` ON `Users` (`Email`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    CREATE INDEX `IX_Users_TenantId` ON `Users` (`TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260521003141_InitialMySql')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260521003141_InitialMySql', '8.0.8');
END;

COMMIT;

START TRANSACTION;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260523214917_AddSocialLoginToUsers')
BEGIN
    ALTER TABLE `Users` ADD `AppleId` longtext NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260523214917_AddSocialLoginToUsers')
BEGIN
    ALTER TABLE `Users` ADD `GoogleId` longtext NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260523214917_AddSocialLoginToUsers')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260523214917_AddSocialLoginToUsers', '8.0.8');
END;

COMMIT;

START TRANSACTION;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260523224522_AddMarketplaceIntegrations')
BEGIN
    CREATE TABLE `StoreIntegrations` (
        `Id` char(36) NOT NULL,
        `Platform` int NOT NULL,
        `DisplayName` varchar(100) NOT NULL,
        `Status` int NOT NULL,
        `ApiKey` varchar(500) NULL,
        `ApiSecret` varchar(500) NULL,
        `RefreshToken` varchar(1000) NULL,
        `StoreUrl` varchar(300) NULL,
        `ExternalStoreId` varchar(100) NULL,
        `WebhookSecret` varchar(500) NULL,
        `TokenExpiresAt` datetime(6) NULL,
        `SyncDirection` int NOT NULL,
        `SyncProducts` tinyint(1) NOT NULL,
        `SyncOrders` tinyint(1) NOT NULL,
        `SyncCustomers` tinyint(1) NOT NULL,
        `SyncInventory` tinyint(1) NOT NULL,
        `SyncPrices` tinyint(1) NOT NULL,
        `AutoSyncIntervalMinutes` int NOT NULL,
        `LastSyncAt` datetime(6) NULL,
        `LastErrorMessage` varchar(1000) NULL,
        `ConsecutiveErrorCount` int NOT NULL,
        `TenantId` char(36) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_StoreIntegrations_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260523224522_AddMarketplaceIntegrations')
BEGIN
    CREATE TABLE `SyncLogs` (
        `Id` char(36) NOT NULL,
        `EntityType` int NOT NULL,
        `Direction` int NOT NULL,
        `Status` int NOT NULL,
        `TotalRecords` int NOT NULL,
        `SuccessCount` int NOT NULL,
        `FailedCount` int NOT NULL,
        `StartedAt` datetime(6) NOT NULL,
        `CompletedAt` datetime(6) NULL,
        `DurationSeconds` double NULL,
        `ErrorMessage` varchar(2000) NULL,
        `Details` longtext NULL,
        `IsManual` tinyint(1) NOT NULL,
        `StoreIntegrationId` char(36) NOT NULL,
        `TenantId` char(36) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_SyncLogs_StoreIntegrations_StoreIntegrationId` FOREIGN KEY (`StoreIntegrationId`) REFERENCES `StoreIntegrations` (`Id`) ON DELETE CASCADE,
        CONSTRAINT `FK_SyncLogs_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260523224522_AddMarketplaceIntegrations')
BEGIN
    CREATE TABLE `WebhookEvents` (
        `Id` char(36) NOT NULL,
        `EventType` varchar(100) NOT NULL,
        `Status` int NOT NULL,
        `RawPayload` longtext NOT NULL,
        `SourceIp` varchar(45) NULL,
        `Signature` varchar(500) NULL,
        `IsVerified` tinyint(1) NOT NULL,
        `RetryCount` int NOT NULL,
        `LastAttemptAt` datetime(6) NULL,
        `ProcessedAt` datetime(6) NULL,
        `ErrorMessage` varchar(2000) NULL,
        `ExternalEntityId` varchar(100) NULL,
        `StoreIntegrationId` char(36) NOT NULL,
        `TenantId` char(36) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_WebhookEvents_StoreIntegrations_StoreIntegrationId` FOREIGN KEY (`StoreIntegrationId`) REFERENCES `StoreIntegrations` (`Id`) ON DELETE CASCADE,
        CONSTRAINT `FK_WebhookEvents_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260523224522_AddMarketplaceIntegrations')
BEGIN
    CREATE INDEX `IX_StoreIntegrations_TenantId` ON `StoreIntegrations` (`TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260523224522_AddMarketplaceIntegrations')
BEGIN
    CREATE INDEX `IX_StoreIntegrations_TenantId_Platform` ON `StoreIntegrations` (`TenantId`, `Platform`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260523224522_AddMarketplaceIntegrations')
BEGIN
    CREATE INDEX `IX_StoreIntegrations_TenantId_Status` ON `StoreIntegrations` (`TenantId`, `Status`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260523224522_AddMarketplaceIntegrations')
BEGIN
    CREATE INDEX `IX_SyncLogs_IntegrationId_Status` ON `SyncLogs` (`StoreIntegrationId`, `Status`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260523224522_AddMarketplaceIntegrations')
BEGIN
    CREATE INDEX `IX_SyncLogs_StoreIntegrationId` ON `SyncLogs` (`StoreIntegrationId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260523224522_AddMarketplaceIntegrations')
BEGIN
    CREATE INDEX `IX_SyncLogs_TenantId` ON `SyncLogs` (`TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260523224522_AddMarketplaceIntegrations')
BEGIN
    CREATE INDEX `IX_SyncLogs_TenantId_CreatedAt` ON `SyncLogs` (`TenantId`, `CreatedAt`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260523224522_AddMarketplaceIntegrations')
BEGIN
    CREATE INDEX `IX_WebhookEvents_IntegrationId_EventType` ON `WebhookEvents` (`StoreIntegrationId`, `EventType`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260523224522_AddMarketplaceIntegrations')
BEGIN
    CREATE INDEX `IX_WebhookEvents_IntegrationId_Status` ON `WebhookEvents` (`StoreIntegrationId`, `Status`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260523224522_AddMarketplaceIntegrations')
BEGIN
    CREATE INDEX `IX_WebhookEvents_StoreIntegrationId` ON `WebhookEvents` (`StoreIntegrationId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260523224522_AddMarketplaceIntegrations')
BEGIN
    CREATE INDEX `IX_WebhookEvents_TenantId` ON `WebhookEvents` (`TenantId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260523224522_AddMarketplaceIntegrations')
BEGIN
    CREATE INDEX `IX_WebhookEvents_TenantId_CreatedAt` ON `WebhookEvents` (`TenantId`, `CreatedAt`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260523224522_AddMarketplaceIntegrations')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260523224522_AddMarketplaceIntegrations', '8.0.8');
END;

COMMIT;

START TRANSACTION;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260524010325_AddExternalIdColumns')
BEGIN
    ALTER TABLE `Products` ADD `ExternalId` longtext NOT NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260524010325_AddExternalIdColumns')
BEGIN
    ALTER TABLE `Products` ADD `StoreIntegrationId` char(36) NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260524010325_AddExternalIdColumns')
BEGIN
    ALTER TABLE `Orders` ADD `ExternalId` longtext NOT NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260524010325_AddExternalIdColumns')
BEGIN
    ALTER TABLE `Orders` ADD `ExternalOrderNumber` longtext NOT NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260524010325_AddExternalIdColumns')
BEGIN
    ALTER TABLE `Orders` ADD `StoreIntegrationId` char(36) NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260524010325_AddExternalIdColumns')
BEGIN
    ALTER TABLE `OrderItems` ADD `ExternalId` longtext NOT NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260524010325_AddExternalIdColumns')
BEGIN
    ALTER TABLE `OrderItems` ADD `ExternalProductId` longtext NOT NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260524010325_AddExternalIdColumns')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260524010325_AddExternalIdColumns', '8.0.8');
END;

COMMIT;

