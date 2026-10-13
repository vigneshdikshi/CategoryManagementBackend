using System.ComponentModel.DataAnnotations;

namespace CategoryManagement.Api.Models;

public class Category
{
    public long Id { get; set; }

    [Required, MaxLength(5)]
    public string CategoryCode { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public long? ParentId { get; set; }
    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();

    [Range(1, 3)]
    public short Level { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
