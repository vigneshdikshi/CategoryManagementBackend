using CategoryManagement.Api.Data;
using CategoryManagement.Api.DTOs;
using CategoryManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CategoryManagement.Api.Services;

public class ProductService(AppDbContext db) : IProductService
{
    public async Task<IReadOnlyList<ProductResponse>> GetAllAsync(
        string? categoryCode, bool includeInactive, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking()
            .Include(p => p.Category)
                .ThenInclude(c => c!.Parent)
                    .ThenInclude(c => c!.Parent)
            .AsQueryable();

        if (!includeInactive) query = query.Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(categoryCode))
        {
            var code = categoryCode.Trim();
            query = query.Where(p => p.Category != null && p.Category.CategoryCode == code);
        }

        var items = await query.OrderBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return items.Select(ToResponse).ToList();
    }

    public async Task<ProductResponse?> GetByIdAsync(long id, CancellationToken ct)
    {
        var item = await db.Products.AsNoTracking()
            .Include(p => p.Category)
                .ThenInclude(c => c!.Parent)
                    .ThenInclude(c => c!.Parent)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        return item is null ? null : ToResponse(item);
    }

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken ct)
    {
        if (request.Id <= 0)
            throw new ArgumentException("Product ID must be greater than 0.");

        var existing = await db.Products.AnyAsync(p => p.Id == request.Id, ct);
        if (existing)
            throw new InvalidOperationException($"Product with ID {request.Id} already exists.");

        var category = await db.Categories
            .Include(c => c.Parent)
                .ThenInclude(c => c!.Parent)
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct);
        if (category is null)
            throw new KeyNotFoundException($"Category ID {request.CategoryId} was not found.");
        if (category.Level < 1 || category.Level > 3)
            throw new InvalidOperationException("Products must be assigned to a Level 1, 2, or 3 category.");
        if (!category.IsActive)
            throw new InvalidOperationException("Cannot add a product to an inactive category.");

        var product = new Product
        {
            Id = request.Id,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Price = request.Price,
            ImageUrl = request.ImageUrl?.Trim(),
            StockQuantity = request.StockQuantity,
            CategoryId = request.CategoryId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        product.Category = category;
        return ToResponse(product);
    }

    public async Task<ProductResponse?> UpdateAsync(long id, UpdateProductRequest request, CancellationToken ct)
    {
        var product = await db.Products
            .Include(p => p.Category)
                .ThenInclude(c => c!.Parent)
                    .ThenInclude(c => c!.Parent)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return null;

        var category = await db.Categories
            .Include(c => c.Parent)
                .ThenInclude(c => c!.Parent)
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct);
        if (category is null)
            throw new KeyNotFoundException($"Category ID {request.CategoryId} was not found.");
        if (category.Level < 1 || category.Level > 3)
            throw new InvalidOperationException("Products must be assigned to a Level 1, 2, or 3 category.");
        if (!category.IsActive)
            throw new InvalidOperationException("Cannot assign a product to an inactive category.");

        product.Name = request.Name.Trim();
        product.Description = request.Description?.Trim();
        product.Price = request.Price;
        product.ImageUrl = request.ImageUrl?.Trim();
        product.StockQuantity = request.StockQuantity;
        product.CategoryId = request.CategoryId;
        product.Category = category;
        product.IsActive = request.IsActive;
        product.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return ToResponse(product);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return false;

        // Soft delete preserves historical product references.
        product.IsActive = false;
        product.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static ProductResponse ToResponse(Product p)
    {
        var cat = p.Category;
        var level = cat?.Level ?? 1;
        var path = GetCategoryPath(cat);
        return new(
            p.Id, p.Name, p.Description, p.Price, p.ImageUrl, p.StockQuantity,
            p.CategoryId, cat?.CategoryCode ?? string.Empty,
            cat?.Name ?? string.Empty, p.IsActive, p.CreatedAtUtc, p.UpdatedAtUtc,
            level, path);
    }

    private static string GetCategoryPath(Category? c)
    {
        if (c == null) return string.Empty;
        var parts = new List<string>();
        var curr = c;
        while (curr != null)
        {
            parts.Insert(0, curr.Name);
            curr = curr.Parent;
        }
        return string.Join(" > ", parts);
    }
}
