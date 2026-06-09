using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Shared.Settings;
using Microsoft.Extensions.Options;

namespace EcomPlatform.Infrastructure.Services
{
    public class CloudinaryFileUploadService : IFileUploadService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryFileUploadService(IOptions<CloudinarySettings> settings)
        {
            var account = new Account(
                settings.Value.CloudName,
                settings.Value.ApiKey,
                settings.Value.ApiSecret);
            _cloudinary = new Cloudinary(account);
        }

        public async Task<ApiResponse<FileUploadResponseDto>> UploadImageAsync(
            Stream fileStream, string fileName, string folder = "general")
        {
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, fileStream),
                Folder = $"ecomplatform/{folder}",
                Transformation = new Transformation()
                    .Quality("auto")
                    .FetchFormat("auto")
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            if (result.Error != null)
                return ApiResponse<FileUploadResponseDto>.Fail(result.Error.Message);

            return ApiResponse<FileUploadResponseDto>.Ok(new FileUploadResponseDto
            {
                Url = result.SecureUrl.ToString(),
                PublicId = result.PublicId,
                Format = result.Format,
                Width = result.Width,
                Height = result.Height,
                Size = result.Bytes
            }, "File uploaded successfully");
        }

        public async Task<ApiResponse<bool>> DeleteFileAsync(string publicId)
        {
            var deleteParams = new DeletionParams(publicId);
            var result = await _cloudinary.DestroyAsync(deleteParams);

            if (result.Result != "ok")
                return ApiResponse<bool>.Fail("Failed to delete file");

            return ApiResponse<bool>.Ok(true, "File deleted successfully");
        }

        public async Task<ApiResponse<List<FileUploadResponseDto>>> UploadMultipleAsync(
            List<(Stream Stream, string FileName)> files, string folder = "general")
        {
            var results = new List<FileUploadResponseDto>();

            foreach (var (stream, fileName) in files)
            {
                var result = await UploadImageAsync(stream, fileName, folder);
                if (!result.Success)
                    return ApiResponse<List<FileUploadResponseDto>>.Fail(result.Message);
                results.Add(result.Data!);
            }

            return ApiResponse<List<FileUploadResponseDto>>.Ok(results,
                $"{results.Count} files uploaded successfully");
        }

        public async Task<ApiResponse<CloudinaryMediaLibraryDto>> GetAllMediaAsync(
            string? folder = null, int maxResults = 50, string? nextCursor = null)
        {
            var searchFolder = string.IsNullOrEmpty(folder)
                ? "ecomplatform"
                : $"ecomplatform/{folder}";

            var searchExpression = $"folder:{searchFolder}/*";

            var search = _cloudinary.Search()
                .Expression(searchExpression)
                .WithField("context")
                .WithField("tags")
                .MaxResults(maxResults);

            if (!string.IsNullOrEmpty(nextCursor))
                search = search.NextCursor(nextCursor);

            var result = await search.ExecuteAsync();

            if (result.Error != null)
                return ApiResponse<CloudinaryMediaLibraryDto>.Fail(result.Error.Message);

            var items = result.Resources.Select(r => new CloudinaryMediaItemDto
            {
                PublicId = r.PublicId,
                Url = r.SecureUrl?.ToString() ?? string.Empty,
                Format = r.Format,
                Width = r.Width,
                Height = r.Height,
                Size = r.Bytes,
                Folder = r.Folder ?? string.Empty,
                CreatedAt = DateTime.TryParse(r.CreatedAt, out var dt) ? dt : DateTime.UtcNow
            }).ToList();

            return ApiResponse<CloudinaryMediaLibraryDto>.Ok(new CloudinaryMediaLibraryDto
            {
                Items = items,
                TotalCount = result.TotalCount,
                NextCursor = result.NextCursor
            }, "Media fetched successfully");
        }
    }
}