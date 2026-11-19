using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.Application.Common.Interfaces;
using SmartStudy.Application.Common.Models;
using SmartStudy.Application.DTOs.Materials;
using SmartStudy.Application.Services;
using SmartStudy.Domain.Exceptions;

namespace SmartStudy.API.Controllers;

/// <summary>
/// Thin Controller điều phối Kho Học liệu PDF và gọi dịch vụ bóc tách tóm tắt AI.
/// </summary>
[ApiController]
[Authorize]
public class MaterialsController : ControllerBase
{
    private readonly IMaterialService _materialService;
    private readonly ICurrentUserService _currentUser;

    public MaterialsController(IMaterialService materialService, ICurrentUserService currentUser)
    {
        _materialService = materialService;
        _currentUser = currentUser;
    }

    [HttpGet("api/rooms/{roomId:int}/materials")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MaterialDto>>>> GetRoomMaterials(
        int roomId,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _materialService.GetRoomMaterialsAsync(roomId, userId, _currentUser.IsAdmin, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<MaterialDto>>.Ok(data));
    }

    [HttpGet("api/materials/{materialId:int}")]
    public async Task<ActionResult<ApiResponse<MaterialDetailDto>>> GetMaterialDetail(
        int materialId,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _materialService.GetMaterialDetailAsync(materialId, userId, _currentUser.IsAdmin, cancellationToken);
        return Ok(ApiResponse<MaterialDetailDto>.Ok(data));
    }

    [HttpPost("api/materials/{materialId:int}/mindmap")]
    public async Task<ActionResult<ApiResponse<MaterialMindmapDto>>> GenerateMaterialMindmap(
        int materialId,
        [FromBody] GenerateMindmapRequestDto? request,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _materialService.GenerateMaterialMindmapAsync(
            materialId,
            userId,
            _currentUser.IsAdmin,
            request?.ForceRegenerate ?? false,
            cancellationToken);
        return Ok(ApiResponse<MaterialMindmapDto>.Ok(data, "Đã tải Sơ đồ Tư duy Kiến thức AI thành công."));
    }

    [HttpPost("api/rooms/{roomId:int}/materials")]
    [RequestSizeLimit(26 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<MaterialDto>>> UploadMaterial(
        int roomId,
        [FromForm] IFormFile? file,
        [FromForm] string? title,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new DomainException("Vui lòng đính kèm tệp học liệu PDF hợp lệ.");
        }

        var userId = RequireUserId();
        await using var stream = file.OpenReadStream();
        var data = await _materialService.UploadMaterialAsync(
            roomId,
            userId,
            title ?? string.Empty,
            stream,
            file.FileName,
            file.Length,
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<MaterialDto>.Ok(data, "Tải lên học liệu PDF và tóm tắt AI thành công."));
    }

    [HttpDelete("api/materials/{materialId:int}")]
    public async Task<ActionResult<ApiResponse>> DeleteMaterial(
        int materialId,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        await _materialService.DeleteMaterialAsync(materialId, userId, _currentUser.IsAdmin, cancellationToken);
        return Ok(ApiResponse.OkMessage("Đã xóa học liệu PDF khỏi phòng học."));
    }

    private int RequireUserId()
    {
        return _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");
    }
}
