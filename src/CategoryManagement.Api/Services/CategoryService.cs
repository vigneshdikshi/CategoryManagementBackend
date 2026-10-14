using System.Data;
using CategoryManagement.Api.Data;
using CategoryManagement.Api.DTOs;
using CategoryManagement.Api.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CategoryManagement.Api.Services;

public class CategoryService(AppDbContext db) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryResponse>> GetAllAsync(
        long? parentId, short? level, bool includeInactive, CancellationToken ct)
    {
        var query = db.Categories.AsNoTracking().Include(c => c.Parent).AsQueryable();

        if (!includeInactive) query = query.Where(c => c.IsActive);
        if (parentId.HasValue) query = query.Where(c => c.ParentId == parentId.Value);
        else if (level == 1) query = query.Where(c => c.ParentId == null);
        if (level.HasValue) query = query.Where(c => c.Level == level.Value);

        var items = await query.OrderBy(c => c.Id).ToListAsync(ct);
        return items.Select(ToResponse).ToList();
    }

    public async Task<CategoryResponse?> GetByIdAsync(long id, CancellationToken ct)
    {
        var item = await db.Categories.AsNoTracking().Include(c => c.Parent)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        return item is null ? null : ToResponse(item);
    }

    public async Task<CategoryResponse?> GetByCodeAsync(string code, CancellationToken ct)
    {
        var normalized = code.Trim();
        var item = await db.Categories.AsNoTracking().Include(c => c.Parent)
            .FirstOrDefaultAsync(c => c.CategoryCode == normalized, ct);
        return item is null ? null : ToResponse(item);
    }

    public async Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken ct)
    {
        if (request.Id <= 0)
            throw new ArgumentException("Category ID must be greater than 0.");

        var existing = await db.Categories.AnyAsync(c => c.Id == request.Id, ct);
        if (existing)
            throw new InvalidOperationException($"Category with ID {request.Id} already exists.");

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Category name cannot be empty.");

        short level = 1;
        Category? parent = null;

        if (request.ParentId.HasValue)
        {
            parent = await db.Categories.FirstOrDefaultAsync(c => c.Id == request.ParentId.Value, ct);
            if (parent is null)
                throw new KeyNotFoundException($"Parent category ID {request.ParentId.Value} was not found.");
            if (!parent.IsActive)
                throw new InvalidOperationException("Cannot create a child under an inactive category.");
            if (parent.Level >= 3)
                throw new InvalidOperationException("Level 3 categories cannot have child categories.");
            level = (short)(parent.Level + 1);
        }

        var resultParam = new SqlParameter
        {
            ParameterName = "@Value",
            SqlDbType = SqlDbType.BigInt,
            Direction = ParameterDirection.Output
        };
        await db.Database.ExecuteSqlRawAsync(
            "SELECT @Value = NEXT VALUE FOR category_code_seq",
            new[] { resultParam },
            ct);
        var nextCodeNumber = (long)(resultParam.Value ?? 0L);

        if (nextCodeNumber > 99999)
        {
            throw new InvalidOperationException(
                "Category code sequence exceeded five digits. Increase the category-code length before creating more categories.");
        }

        var category = new Category
        {
            Id = request.Id,
            CategoryCode = nextCodeNumber.ToString("D5"),
            Name = name,
            ParentId = parent?.Id,
            Level = level,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);

        category.Parent = parent;
        return ToResponse(category);
    }

    public async Task<CategoryResponse?> UpdateAsync(long id, UpdateCategoryRequest request, CancellationToken ct)
    {
        var category = await db.Categories.Include(c => c.Parent).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return null;

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Category name cannot be empty.");

        category.Name = name;
        category.IsActive = request.IsActive;
        category.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToResponse(category);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return false;

        var hasChildren = await db.Categories.AnyAsync(c => c.ParentId == id, ct);
        if (hasChildren)
            throw new InvalidOperationException("Cannot delete a category that has child categories. Delete or reassign its children first.");

        var hasProducts = await db.Products.AnyAsync(p => p.CategoryId == id, ct);
        if (hasProducts)
            throw new InvalidOperationException("Cannot delete a category that has products. Reassign or remove its products first.");

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static CategoryResponse ToResponse(Category c) => new(
        c.Id, c.CategoryCode, c.Name, c.ParentId, c.Parent?.CategoryCode,
        c.Level, c.IsActive, c.CreatedAtUtc, c.UpdatedAtUtc);
}
