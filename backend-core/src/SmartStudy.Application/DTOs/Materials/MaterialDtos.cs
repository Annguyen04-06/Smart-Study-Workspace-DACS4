namespace SmartStudy.Application.DTOs.Materials;

/// <summary>
/// Các DTO đại diện cho Kho Học liệu PDF và bản Tóm tắt Học thuật AI của phòng học.
/// </summary>
public class UploadMaterialMetadataDto
{
    public string Title { get; set; } = string.Empty;
}

public class MaterialDto
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public int UploadedById { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int PageCount { get; set; }
    public string? AiSummary { get; set; }
    public string? AiMindmap { get; set; }
    public bool HasExtractedText { get; set; }
    public bool IsApproved { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MaterialDetailDto : MaterialDto
{
    public string? ExtractedText { get; set; }
}

public class GenerateMindmapRequestDto
{
    public bool ForceRegenerate { get; set; }
}

public class MaterialMindmapDto
{
    public int MaterialId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string MermaidCode { get; set; } = string.Empty;
    public int NodeCount { get; set; }
}
