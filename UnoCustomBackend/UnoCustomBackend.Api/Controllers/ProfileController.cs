using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UnoCustomBackend.Api.Models.Requests;
using UnoCustomBackend.Api.Models.Responses;
using UnoCustomBackend.Api.Services;

namespace UnoCustomBackend.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly FirebaseDatabaseService _firebaseDatabaseService;

        public ProfileController(FirebaseDatabaseService firebaseDatabaseService)
        {
            _firebaseDatabaseService = firebaseDatabaseService;
        }

        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            string? userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new ApiResponse
                {
                    Success = false,
                    Message = "Token không hợp lệ."
                });
            }

            var user = await _firebaseDatabaseService.GetUserByIdAsync(userId);

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Lấy thông tin thành công.",
                Data = new
                {
                    user?.Id,
                    user?.Email,
                    user?.Username,
                    user?.DisplayName,
                    user?.CreatedAt,
                    user?.LastLoginAt
                }
            });
        }

        [HttpPost("set-display-name")]
        public async Task<IActionResult> SetDisplayName(SetDisplayNameRequest request)
        {
            string? userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new ApiResponse
                {
                    Success = false,
                    Message = "Token không hợp lệ."
                });
            }

            await _firebaseDatabaseService.UpdateDisplayNameAsync(userId, request.DisplayName);

            return Ok(new ApiResponse
            {
                Success = true,
                Message = "Cập nhật tên hiển thị thành công."
            });
        }
    }
}