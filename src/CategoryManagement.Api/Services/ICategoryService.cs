using CategoryManagement.Api.DTOs;

namespace CategoryManagement.Api.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryResponse>> GetAllAsync(long? parentId, short? level, bool includeInactive, CancellationToken ct);
    Task<CategoryResponse?> GetByIdAsync(long id, CancellationToken ct);
    Task<CategoryResponse?> GetByCodeAsync(string code, CancellationToken ct);
    Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken ct);
    Task<CategoryResponse?> UpdateAsync(long id, UpdateCategoryRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(long id, CancellationToken ct);
}
