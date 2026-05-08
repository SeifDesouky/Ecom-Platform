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
}