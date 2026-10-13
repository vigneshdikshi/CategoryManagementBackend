using System.ComponentModel.DataAnnotations;

namespace CategoryManagement.Api.Models;

public class Product
{
    public long Id { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Range(0, 999999999)]
    public decimal Price { get; set; }

    [MaxLength(1000)]
    public string? ImageUrl { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    public long CategoryId { get; set; }
    public Category? Category { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
