using EcomPlatform.Application.Common;
using EcomPlatform.Application.Common.Interfaces;
using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class IntegrationService : IIntegrationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAdapterFactory _adapterFactory;
        private readonly IEncryptionService _encryption;
        private readonly ISyncService _syncService;

        public IntegrationService(
            IUnitOfWork unitOfWork,
            IAdapterFactory adapterFactory,
            IEncryptionService encryption,
            ISyncService syncService)
        {
            _unitOfWork = unitOfWork;
            _adapterFactory = adapterFactory;
            _encryption = encryption;
            _syncService = syncService;
        }

        // ── CRUD ─────────────────────────────────────────────────────────────

        public async Task<ApiResponse<IntegrationDto>> CreateAsync(
            CreateIntegrationDto dto,
            Guid tenantId,
            CancellationToken ct = default)
        {
            if (!_adapterFactory.IsSupported(dto.Platform))
                return ApiResponse<IntegrationDto>.Fail($"Platform {dto.Platform} is not supported yet");

            var existing = await _unitOfWork.StoreIntegrations
                .FindAsync(i => i.TenantId == tenantId
                             && i.Platform == dto.Platform
                             && !i.IsDeleted);

            if (existing.Any())
                return ApiResponse<IntegrationDto>.Fail(
                    $"Integration with {dto.Platform} already exists");

            var integration = new StoreIntegration
            {
                TenantId = tenantId,
                Platform = dto.Platform,
                DisplayName = dto.DisplayName,
                ApiKey = _encryption.Encrypt(dto.ApiKey),
                ApiSecret = _encryption.Encrypt(dto.ApiSecret),
                StoreUrl = dto.StoreUrl,
                ExternalStoreId = dto.ExternalStoreId,
                SyncDirection = dto.SyncDirection,
                SyncProducts = dto.SyncProducts,
                SyncOrders = dto.SyncOrders,
                SyncCustomers = dto.SyncCustomers,
                SyncInventory = dto.SyncInventory,
                SyncPrices = dto.SyncPrices,
                AutoSyncIntervalMinutes = dto.AutoSyncIntervalMinutes,
                Status = IntegrationStatus.Active
            };

            await _unitOfWork.StoreIntegrations.AddAsync(integration);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<IntegrationDto>.Ok(
                MapToDto(integration), "Integration created successfully");
        }

        public async Task<ApiResponse<IntegrationDto>> GetByIdAsync(
            Guid id,
            CancellationToken ct = default)
        {
            var integration = await _unitOfWork.StoreIntegrations.GetByIdAsync(id);
            if (integration is null || integration.IsDeleted)
                return ApiResponse<IntegrationDto>.Fail("Integration not found");

            return ApiResponse<IntegrationDto>.Ok(MapToDto(integration));
        }

        // Tenant-scoped
        public async Task<ApiResponse<IReadOnlyList<IntegrationDto>>> GetAllAsync(
            Guid tenantId,
            CancellationToken ct = default)
        {
            var integrations = await _unitOfWork.StoreIntegrations
                .FindAsync(i => i.TenantId == tenantId && !i.IsDeleted);

            var result = integrations
                .OrderByDescending(i => i.CreatedAt)
                .Select(MapToDto)
                .ToList();

            return ApiResponse<IReadOnlyList<IntegrationDto>>.Ok(result);
        }

        // ✅ SuperAdmin — كل الـ integrations بدون تصفية بالـ tenant
        public async Task<ApiResponse<IReadOnlyList<IntegrationDto>>> GetAllAsync(
            CancellationToken ct = default)
        {
            var integrations = await _unitOfWork.StoreIntegrations
                .FindAsync(i => !i.IsDeleted);

            var result = integrations
                .OrderByDescending(i => i.CreatedAt)
                .Select(MapToDto)
                .ToList();

            return ApiResponse<IReadOnlyList<IntegrationDto>>.Ok(result);
        }

        public async Task<ApiResponse<IntegrationDto>> UpdateAsync(
            Guid id,
            UpdateIntegrationDto dto,
            CancellationToken ct = default)
        {
            var integration = await _unitOfWork.StoreIntegrations.GetByIdAsync(id);
            if (integration is null || integration.IsDeleted)
                return ApiResponse<IntegrationDto>.Fail("Integration not found");

            if (dto.DisplayName is not null)
                integration.DisplayName = dto.DisplayName;
            if (dto.ApiKey is not null)
                integration.ApiKey = _encryption.Encrypt(dto.ApiKey);
            if (dto.ApiSecret is not null)
                integration.ApiSecret = _encryption.Encrypt(dto.ApiSecret);
            if (dto.StoreUrl is not null)
                integration.StoreUrl = dto.StoreUrl;
            if (dto.ExternalStoreId is not null)
                integration.ExternalStoreId = dto.ExternalStoreId;
            if (dto.SyncProducts.HasValue)
                integration.SyncProducts = dto.SyncProducts.Value;
            if (dto.SyncOrders.HasValue)
                integration.SyncOrders = dto.SyncOrders.Value;
            if (dto.SyncCustomers.HasValue)
                integration.SyncCustomers = dto.SyncCustomers.Value;
            if (dto.SyncInventory.HasValue)
                integration.SyncInventory = dto.SyncInventory.Value;
            if (dto.SyncPrices.HasValue)
                integration.SyncPrices = dto.SyncPrices.Value;
            if (dto.AutoSyncIntervalMinutes.HasValue)
                integration.AutoSyncIntervalMinutes = dto.AutoSyncIntervalMinutes.Value;

            integration.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.StoreIntegrations.UpdateAsync(integration);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<IntegrationDto>.Ok(
                MapToDto(integration), "Integration updated successfully");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(
            Guid id,
            CancellationToken ct = default)
        {
            var integration = await _unitOfWork.StoreIntegrations.GetByIdAsync(id);
            if (integration is null || integration.IsDeleted)
                return ApiResponse<bool>.Fail("Integration not found");

            integration.IsDeleted = true;
            integration.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.StoreIntegrations.UpdateAsync(integration);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Integration deleted successfully");
        }

        // ── Connection ───────────────────────────────────────────────────────

        public async Task<ApiResponse<AdapterResult>> TestConnectionAsync(
            Guid integrationId,
            CancellationToken ct = default)
        {
            var integration = await _unitOfWork.StoreIntegrations.GetByIdAsync(integrationId);
            if (integration is null || integration.IsDeleted)
                return ApiResponse<AdapterResult>.Fail("Integration not found");

            var decrypted = DecryptSensitiveFields(integration);
            var adapter = _adapterFactory.GetAdapter(integration.Platform);
            var result = await adapter.TestConnectionAsync(decrypted, ct);

            if (result.IsSuccess)
            {
                integration.Status = IntegrationStatus.Active;
                integration.ConsecutiveErrorCount = 0;
                integration.LastErrorMessage = null;
            }
            else
            {
                integration.Status = IntegrationStatus.Error;
                integration.ConsecutiveErrorCount++;
                integration.LastErrorMessage = result.ErrorMessage;
            }

            integration.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.StoreIntegrations.UpdateAsync(integration);
            await _unitOfWork.SaveChangesAsync();

            // ✅ الإصلاح: رجّع success أو fail حسب نتيجة الـ test
            return result.IsSuccess
                ? ApiResponse<AdapterResult>.Ok(result, "Connection successful")
                : ApiResponse<AdapterResult>.Fail(result.ErrorMessage ?? "Connection failed");
        }

        public async Task<ApiResponse<bool>> ActivateAsync(
            Guid integrationId,
            CancellationToken ct = default)
        {
            var integration = await _unitOfWork.StoreIntegrations.GetByIdAsync(integrationId);
            if (integration is null || integration.IsDeleted)
                return ApiResponse<bool>.Fail("Integration not found");

            integration.Status = IntegrationStatus.Active;
            integration.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.StoreIntegrations.UpdateAsync(integration);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Integration activated successfully");
        }

        public async Task<ApiResponse<bool>> DeactivateAsync(
            Guid integrationId,
            CancellationToken ct = default)
        {
            var integration = await _unitOfWork.StoreIntegrations.GetByIdAsync(integrationId);
            if (integration is null || integration.IsDeleted)
                return ApiResponse<bool>.Fail("Integration not found");

            integration.Status = IntegrationStatus.Inactive;
            integration.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.StoreIntegrations.UpdateAsync(integration);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Integration deactivated successfully");
        }

        // ── Sync ─────────────────────────────────────────────────────────────

        public async Task<ApiResponse<SyncResultDto>> SyncAsync(
            Guid integrationId,
            SyncEntityType entityType,
            SyncDirection direction,
            bool isManual = true,
            CancellationToken ct = default)
        {
            var integration = await _unitOfWork.StoreIntegrations.GetByIdAsync(integrationId);
            if (integration is null || integration.IsDeleted)
                return ApiResponse<SyncResultDto>.Fail("Integration not found");

            if (integration.Status != IntegrationStatus.Active)
                return ApiResponse<SyncResultDto>.Fail("Integration is not active");

            var decrypted = DecryptSensitiveFields(integration);

            var result = await _syncService.SyncAsync(
                decrypted, entityType, direction, isManual, ct);

            return ApiResponse<SyncResultDto>.Ok(result);
        }

        public async Task<ApiResponse<IReadOnlyList<SyncLogDto>>> GetSyncLogsAsync(
            Guid integrationId,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default)
        {
            var logs = await _unitOfWork.SyncLogs
                .FindAsync(l => l.StoreIntegrationId == integrationId);

            var result = logs
                .OrderByDescending(l => l.StartedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(MapToSyncLogDto)
                .ToList();

            return ApiResponse<IReadOnlyList<SyncLogDto>>.Ok(result);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private StoreIntegration DecryptSensitiveFields(StoreIntegration i) => new()
        {
            Id = i.Id,
            TenantId = i.TenantId,
            Platform = i.Platform,
            DisplayName = i.DisplayName,
            Status = i.Status,
            ApiKey = _encryption.Decrypt(i.ApiKey),
            ApiSecret = _encryption.Decrypt(i.ApiSecret),
            RefreshToken = _encryption.Decrypt(i.RefreshToken),
            WebhookSecret = _encryption.Decrypt(i.WebhookSecret),
            StoreUrl = i.StoreUrl,
            ExternalStoreId = i.ExternalStoreId,
            TokenExpiresAt = i.TokenExpiresAt,
            SyncDirection = i.SyncDirection,
            SyncProducts = i.SyncProducts,
            SyncOrders = i.SyncOrders,
            SyncCustomers = i.SyncCustomers,
            SyncInventory = i.SyncInventory,
            SyncPrices = i.SyncPrices,
            AutoSyncIntervalMinutes = i.AutoSyncIntervalMinutes,
            LastSyncAt = i.LastSyncAt,
            LastErrorMessage = i.LastErrorMessage,
            ConsecutiveErrorCount = i.ConsecutiveErrorCount
        };

        // ── Mapping ──────────────────────────────────────────────────────────

        private static IntegrationDto MapToDto(StoreIntegration i) => new()
        {
            Id = i.Id,
            Platform = i.Platform,
            DisplayName = i.DisplayName,
            Status = i.Status,
            SyncDirection = i.SyncDirection,
            SyncProducts = i.SyncProducts,
            SyncOrders = i.SyncOrders,
            SyncCustomers = i.SyncCustomers,
            SyncInventory = i.SyncInventory,
            SyncPrices = i.SyncPrices,
            AutoSyncIntervalMinutes = i.AutoSyncIntervalMinutes,
            LastSyncAt = i.LastSyncAt,
            LastErrorMessage = i.LastErrorMessage,
            ConsecutiveErrorCount = i.ConsecutiveErrorCount,
            CreatedAt = i.CreatedAt,
            UpdatedAt = i.UpdatedAt
        };

        private static SyncResultDto MapToSyncResultDto(SyncLog l) => new()
        {
            SyncLogId = l.Id,
            EntityType = l.EntityType,
            Direction = l.Direction,
            Status = l.Status,
            TotalRecords = l.TotalRecords,
            SuccessCount = l.SuccessCount,
            FailedCount = l.FailedCount,
            DurationSeconds = l.DurationSeconds,
            ErrorMessage = l.ErrorMessage,
            StartedAt = l.StartedAt,
            CompletedAt = l.CompletedAt
        };

        private static SyncLogDto MapToSyncLogDto(SyncLog l) => new()
        {
            Id = l.Id,
            EntityType = l.EntityType,
            Direction = l.Direction,
            Status = l.Status,
            TotalRecords = l.TotalRecords,
            SuccessCount = l.SuccessCount,
            FailedCount = l.FailedCount,
            DurationSeconds = l.DurationSeconds,
            ErrorMessage = l.ErrorMessage,
            IsManual = l.IsManual,
            StartedAt = l.StartedAt,
            CompletedAt = l.CompletedAt
        };
    }
}