using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace UnoGame.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class BaseController : ControllerBase
{
    protected string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User not authenticated");

    protected string? CurrentUserEmail =>
        User.FindFirstValue(ClaimTypes.Email);

    // ─── Unified response helpers ────────────────────────────────────

    protected static IActionResult Ok<T>(T data, string? message = null) =>
        new OkObjectResult(ApiResponse<T>.Ok(data, message));

    protected static IActionResult Created<T>(string location, T data) =>
        new CreatedResult(location, ApiResponse<T>.Ok(data));

    protected static new IActionResult NoContent() =>
        new NoContentResult();

    protected static IActionResult BadRequest(string error) =>
        new BadRequestObjectResult(ApiResponse.Fail(error));

    protected static IActionResult NotFound(string error = "Resource not found") =>
        new NotFoundObjectResult(ApiResponse.Fail(error));

    protected static IActionResult Forbidden(string error = "Access denied") =>
        new ObjectResult(ApiResponse.Fail(error)) { StatusCode = 403 };

    protected static IActionResult Conflict(string error) =>
        new ConflictObjectResult(ApiResponse.Fail(error));

    protected static IActionResult Paginated<T>(
        IEnumerable<T> items,
        int totalCount,
        int page,
        int pageSize)
    {
        var result = PagedResult<T>.Create(items, totalCount, page, pageSize);
        return new OkObjectResult(ApiResponse<PagedResult<T>>.Ok(result));
    }
}

// ─── Response envelope ───────────────────────────────────────────────────

public class ApiResponse
{
    public bool    Success   { get; set; }
    public string? Message   { get; set; }
    public string? Error     { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ApiResponse Fail(string error) =>
        new() { Success = false, Error = error };
}

public class ApiResponse<T> : ApiResponse
{
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null)
    {
        var r = new ApiResponse<T>();
        r.Success = true;
        r.Data    = data;
        r.Message = message;
        return r;
    }
}

public class PagedResult<T>
{
    public IEnumerable<T> Items { get; init; } = Enumerable.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;

    public static PagedResult<T> Create(IEnumerable<T> items, int total, int page, int size) =>
        new() { Items = items, TotalCount = total, Page = page, PageSize = size };
}