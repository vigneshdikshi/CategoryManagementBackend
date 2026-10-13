using System.ComponentModel.DataAnnotations;

namespace CategoryManagement.Api.DTOs;

public sealed class CreateCategoryRequest
{
    [Required]
    [Range(1, long.MaxValue, ErrorMessage = "Category ID must be greater than 0.")]
    public long Id { get; set; }

    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    public long? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateCategoryRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public sealed record CategoryResponse(
    long Id,
    string CategoryCode,
    string Name,
    long? ParentId,
    string? ParentCategoryCode,
    short Level,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
