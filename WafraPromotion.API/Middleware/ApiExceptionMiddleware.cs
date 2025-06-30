using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace WafraPromotion.API.Middleware
{
    public class ApiExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiExceptionMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred.");

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

                var response = _env.IsDevelopment()
                    ? new ApiExceptionResponse((int)HttpStatusCode.InternalServerError, ex.Message, ex.StackTrace?.ToString())
                    : new ApiExceptionResponse((int)HttpStatusCode.InternalServerError, "An internal server error has occurred.");

                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var json = JsonSerializer.Serialize(response, options);

                await context.Response.WriteAsync(json);
            }
        }
    }

    public class ApiExceptionResponse
    {
        public int StatusCode { get; }
        public string Message { get; }
        public string? Details { get; }

        public ApiExceptionResponse(int statusCode, string message, string? details = null)
        {
            StatusCode = statusCode;
            Message = message;
            Details = details;
        }
    }
}
