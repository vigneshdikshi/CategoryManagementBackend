using CategoryManagement.Api.DTOs;

namespace CategoryManagement.Api.Services;

public interface IProductService
{
    Task<IReadOnlyList<ProductResponse>> GetAllAsync(string? categoryCode, bool includeInactive, int page, int pageSize, CancellationToken ct);
    Task<ProductResponse?> GetByIdAsync(long id, CancellationToken ct);
    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken ct);
    Task<ProductResponse?> UpdateAsync(long id, UpdateProductRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(long id, CancellationToken ct);
}
