using System.Net;
using System.Text.Json;
using UnoGame.API.Controllers;
using UnoGame.API.Services;

namespace UnoGame.API.Middleware;

/// <summary>
/// Bắt mọi exception chưa được xử lý.
/// Vị trí: ĐẦU TIÊN trong pipeline (bao tất cả middleware khác).
/// Trả về chuẩn ApiResponse để client parse nhất quán.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _log;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> log)
    {
        _next = next;
        _log  = log;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex) when (!ctx.Response.HasStarted)
        {
            _log.LogError(ex, "Unhandled exception [{Method} {Path}]",
                ctx.Request.Method, ctx.Request.Path);
            await WriteErrorResponseAsync(ctx, ex);
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext ctx, Exception ex)
    {
        var (status, code, message) = ex switch
        {
            AuthException ae => ae.Code switch
            {
                AuthErrorCode.InvalidToken     => (401, "INVALID_TOKEN",    ae.Message),
                AuthErrorCode.TokenExpired     => (401, "TOKEN_EXPIRED",    ae.Message),
                AuthErrorCode.TokenRevoked     => (401, "TOKEN_REVOKED",    ae.Message),
                AuthErrorCode.TokenBlacklisted => (401, "TOKEN_BLACKLISTED",ae.Message),
                AuthErrorCode.UserNotFound     => (404, "USER_NOT_FOUND",   ae.Message),
                AuthErrorCode.EmailNotVerified => (403, "EMAIL_NOT_VERIFIED", ae.Message),
                AuthErrorCode.WeakPassword     => (400, "WEAK_PASSWORD",    ae.Message),
                _                              => (400, "AUTH_ERROR",       ae.Message),
            },
            UnauthorizedAccessException  => (401, "UNAUTHORIZED",    "Authentication required"),
            KeyNotFoundException         => (404, "NOT_FOUND",       ex.Message),
            InvalidOperationException    => (400, "INVALID_OPERATION",ex.Message),
            ArgumentException            => (400, "BAD_ARGUMENT",     ex.Message),
            TimeoutException             => (503, "TIMEOUT",          "Service temporarily unavailable"),
            _                            => (500, "INTERNAL_ERROR",   "An unexpected error occurred")
        };

        ctx.Response.StatusCode  = status;
        ctx.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(new
        {
            success   = false,
            error     = message,
            code      = code,
            timestamp = DateTime.UtcNow
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await ctx.Response.WriteAsync(body);
    }
}
