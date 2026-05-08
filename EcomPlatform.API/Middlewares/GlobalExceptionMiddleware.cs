using EcomPlatform.Application.Common;
using System.Net;
using System.Text.Json;

namespace EcomPlatform.API.Middlewares
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unhandled exception on {Method} {Path}",
                    context.Request.Method,
                    context.Request.Path);

                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(
            HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var (statusCode, message) = exception switch
            {
                ArgumentNullException =>
                    (HttpStatusCode.BadRequest, "Required value was missing."),

                ArgumentException =>
                    (HttpStatusCode.BadRequest, exception.Message),

                KeyNotFoundException =>
                    (HttpStatusCode.NotFound, "Resource not found."),

                UnauthorizedAccessException =>
                    (HttpStatusCode.Unauthorized, "Unauthorized access."),

                InvalidOperationException =>
                    (HttpStatusCode.BadRequest, exception.Message),

                _ =>
                    (HttpStatusCode.InternalServerError,
                     "An unexpected error occurred. Please try again later.")
            };

            context.Response.StatusCode = (int)statusCode;

            var response = ApiResponse<object>.Fail(
                message,
                new List<string> { exception.GetType().Name });

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
    }
}