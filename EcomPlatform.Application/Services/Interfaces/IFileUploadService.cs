using EcomPlatform.Application.Common;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IFileUploadService
    {
        Task<ApiResponse<FileUploadResponseDto>> UploadImageAsync(
            Stream fileStream, string fileName, string folder = "general");
        Task<ApiResponse<bool>> DeleteFileAsync(string publicId);
        Task<ApiResponse<List<FileUploadResponseDto>>> UploadMultipleAsync(
            List<(Stream Stream, string FileName)> files, string folder = "general");

        // Super Admin
        Task<ApiResponse<CloudinaryMediaLibraryDto>> GetAllMediaAsync(
            string? folder = null, int maxResults = 50, string? nextCursor = null);
    }

    public class FileUploadResponseDto
    {
        public string Url { get; set; } = string.Empty;
        public string PublicId { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public long Size { get; set; }
    }

    public class CloudinaryMediaLibraryDto
    {
        public List<CloudinaryMediaItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public string? NextCursor { get; set; }
    }

    public class CloudinaryMediaItemDto
    {
        public string PublicId { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string Format { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public long Size { get; set; }
        public string? Folder { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}