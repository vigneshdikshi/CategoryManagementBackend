using System.ComponentModel.DataAnnotations;

namespace CategoryManagement.Api.DTOs;

public sealed class CreateProductRequest
{
    [Required]
    [Range(1, long.MaxValue, ErrorMessage = "Product ID must be greater than 0.")]
    public long Id { get; set; }

    [Required, StringLength(150, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal Price { get; set; }

    [Url]
    [StringLength(1000)]
    public string? ImageUrl { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    [Range(1, long.MaxValue)]
    public long CategoryId { get; set; }
}

public sealed class UpdateProductRequest
{
    [Required, StringLength(150, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal Price { get; set; }

    [Url]
    [StringLength(1000)]
    public string? ImageUrl { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    [Range(1, long.MaxValue)]
    public long CategoryId { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed record ProductResponse(
    long Id,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    int StockQuantity,
    long CategoryId,
    string CategoryCode,
    string CategoryName,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
