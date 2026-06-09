using Asp.Versioning;
using EcomPlatform.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class UploadsController : ControllerBase
    {
        private readonly IFileUploadService _uploadService;

        public UploadsController(IFileUploadService uploadService)
        {
            _uploadService = uploadService;
        }

        [HttpPost("image")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> UploadImage(
            IFormFile file,
            [FromQuery] string folder = "general")
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided");

            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
                return BadRequest("Only JPEG, PNG, and WebP images are allowed");

            using var stream = file.OpenReadStream();
            var result = await _uploadService.UploadImageAsync(
                stream, file.FileName, folder);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpPost("images")]
        [RequestSizeLimit(50 * 1024 * 1024)]
        public async Task<IActionResult> UploadMultipleImages(
            List<IFormFile> files,
            [FromQuery] string folder = "general")
        {
            if (files == null || !files.Any())
                return BadRequest("No files provided");

            if (files.Count > 10)
                return BadRequest("Maximum 10 files allowed");

            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
            foreach (var file in files)
            {
                if (!allowedTypes.Contains(file.ContentType.ToLower()))
                    return BadRequest($"File {file.FileName} is not allowed");
            }

            var fileList = files.Select(f =>
                (f.OpenReadStream(), f.FileName)).ToList();

            var result = await _uploadService.UploadMultipleAsync(fileList, folder);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteFile([FromQuery] string publicId)
        {
            if (string.IsNullOrEmpty(publicId))
                return BadRequest("Public ID is required");

            var result = await _uploadService.DeleteFileAsync(publicId);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        // ============ Super Admin ============

        [HttpGet("admin/media")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetAllMedia(
            [FromQuery] string? folder = null,
            [FromQuery] int maxResults = 50,
            [FromQuery] string? nextCursor = null)
        {
            var result = await _uploadService.GetAllMediaAsync(folder, maxResults, nextCursor);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpDelete("admin/media")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> AdminDeleteFile([FromQuery] string publicId)
        {
            if (string.IsNullOrEmpty(publicId))
                return BadRequest("Public ID is required");

            var result = await _uploadService.DeleteFileAsync(publicId);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }
    }
}