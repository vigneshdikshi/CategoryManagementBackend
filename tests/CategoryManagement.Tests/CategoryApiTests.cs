using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CategoryManagement.Api;
using CategoryManagement.Api.Data;
using CategoryManagement.Api.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CategoryManagement.Tests;

public class TestApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            var connection = Environment.GetEnvironmentVariable(
                "ConnectionStrings__TestConnection")
                ?? Environment.GetEnvironmentVariable(
                    "ConnectionStrings__DefaultConnection")
                ?? Environment.GetEnvironmentVariable(
                    "ConnectionStrings__DefaultConnection",
                    EnvironmentVariableTarget.User)
                ?? Environment.GetEnvironmentVariable(
                    "ConnectionStrings__DefaultConnection",
                    EnvironmentVariableTarget.Machine);

            if (string.IsNullOrWhiteSpace(connection) ||
                !connection.Contains("Database=CategoryManagementDB_Test", StringComparison.OrdinalIgnoreCase))
            {
                var baseConn = context.Configuration.GetConnectionString("DefaultConnection")
                    ?? "Server=.;Database=DIKSHIKART;Integrated Security=True;TrustServerCertificate=True;";
                connection = System.Text.RegularExpressions.Regex.Replace(
                    baseConn,
                    @"Database=[^;]+",
                    "Database=CategoryManagementDB_Test",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            if (string.IsNullOrWhiteSpace(connection) ||
                !connection.Contains(
                    "Database=CategoryManagementDB_Test",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Tests require the dedicated CategoryManagementDB_Test database.");
            }

            config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connection
                });
        });
    }
}

public class CategoryApiTests : IClassFixture<TestApiFactory>
{
    private static long _testIdCounter = 200000;
    private static long GetUniqueTestId() => Interlocked.Increment(ref _testIdCounter);

    private readonly HttpClient _client;
    private readonly TestApiFactory _factory;

    public CategoryApiTests(TestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("http://localhost"),
                AllowAutoRedirect = false
            });
    }

    #region Original Passing Tests

    [Fact]
    public async Task GetCategories_ShouldReturn200()
    {
        var response = await _client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetProducts_ShouldReturn200()
    {
        var response = await _client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task InvalidCategoryLevel_ShouldReturn400()
    {
        var response = await _client.GetAsync(
            "/api/categories?level=4");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InvalidProductPage_ShouldReturn400()
    {
        var response = await _client.GetAsync(
            "/api/products?page=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingCategory_ShouldReturn404()
    {
        var response = await _client.GetAsync(
            "/api/categories/999999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MissingProduct_ShouldReturn404()
    {
        var response = await _client.GetAsync(
            "/api/products/999999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateAndDeleteCategory_ShouldWork()
    {
        var id = GetUniqueTestId();
        var name = "Automated Test " + Guid.NewGuid().ToString("N")[..8];

        var createResponse = await _client.PostAsJsonAsync(
            "/api/categories",
            new
            {
                id,
                name,
                parentId = (long?)null,
                isActive = true
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var document = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var createdId = document.RootElement.GetProperty("id").GetInt64();
        Assert.Equal(id, createdId);

        try
        {
            var getResponse = await _client.GetAsync(
                $"/api/categories/{id}");

            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            var deleteResponse = await _client.DeleteAsync(
                $"/api/categories/{id}");

            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var afterDelete = await _client.GetAsync(
                $"/api/categories/{id}");

            Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
        }
        finally
        {
            // Best-effort cleanup if an assertion failed earlier.
            await _client.DeleteAsync($"/api/categories/{id}");
        }
    }

    #endregion

    #region Category Integration Tests

    [Fact]
    public async Task CreateCategory_ValidLevel1_ShouldSucceed()
    {
        var id = GetUniqueTestId();
        var name = "L1_" + Guid.NewGuid().ToString("N")[..8];
        long? catId = null;
        try
        {
            var response = await _client.PostAsJsonAsync(
                "/api/categories",
                new CreateCategoryRequest
                {
                    Id = id,
                    Name = name,
                    ParentId = null,
                    IsActive = true
                });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var category = await response.Content.ReadFromJsonAsync<CategoryResponse>();
            Assert.NotNull(category);
            catId = category.Id;

            Assert.Equal(id, category.Id);
            Assert.Equal(name, category.Name);
            Assert.Null(category.ParentId);
            Assert.Null(category.ParentCategoryCode);
            Assert.Equal((short)1, category.Level);
            Assert.True(category.IsActive);
            Assert.Matches(@"^\d{5}$", category.CategoryCode);

            var getResponse = await _client.GetAsync($"/api/categories/{category.Id}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        }
        finally
        {
            if (catId.HasValue) await DeleteCategorySafeAsync(catId.Value);
        }
    }

    [Fact]
    public async Task CreateCategory_Level2UnderLevel1_ShouldSucceed()
    {
        var l1 = await CreateCategoryAsync();
        var l2Id = GetUniqueTestId();
        try
        {
            var name = "L2_" + Guid.NewGuid().ToString("N")[..8];
            var response = await _client.PostAsJsonAsync(
                "/api/categories",
                new CreateCategoryRequest
                {
                    Id = l2Id,
                    Name = name,
                    ParentId = l1.Id,
                    IsActive = true
                });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var l2 = await response.Content.ReadFromJsonAsync<CategoryResponse>();
            Assert.NotNull(l2);

            Assert.Equal(l2Id, l2.Id);
            Assert.Equal(name, l2.Name);
            Assert.Equal(l1.Id, l2.ParentId);
            Assert.Equal(l1.CategoryCode, l2.ParentCategoryCode);
            Assert.Equal((short)2, l2.Level);
            Assert.True(l2.IsActive);
            Assert.Matches(@"^\d{5}$", l2.CategoryCode);
        }
        finally
        {
            await DeleteCategorySafeAsync(l2Id);
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    [Fact]
    public async Task CreateCategory_Level3UnderLevel2_ShouldSucceed()
    {
        var l1 = await CreateCategoryAsync();
        var l2 = await CreateCategoryAsync(parentId: l1.Id);
        var l3Id = GetUniqueTestId();
        try
        {
            var name = "L3_" + Guid.NewGuid().ToString("N")[..8];
            var response = await _client.PostAsJsonAsync(
                "/api/categories",
                new CreateCategoryRequest
                {
                    Id = l3Id,
                    Name = name,
                    ParentId = l2.Id,
                    IsActive = true
                });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var l3 = await response.Content.ReadFromJsonAsync<CategoryResponse>();
            Assert.NotNull(l3);

            Assert.Equal(l3Id, l3.Id);
            Assert.Equal(name, l3.Name);
            Assert.Equal(l2.Id, l3.ParentId);
            Assert.Equal(l2.CategoryCode, l3.ParentCategoryCode);
            Assert.Equal((short)3, l3.Level);
            Assert.True(l3.IsActive);
            Assert.Matches(@"^\d{5}$", l3.CategoryCode);
        }
        finally
        {
            await DeleteCategorySafeAsync(l3Id);
            await DeleteCategorySafeAsync(l2.Id);
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    [Fact]
    public async Task CreateCategory_ChildUnderLevel3_ShouldReturn409Conflict()
    {
        var (l1, l2, l3) = await CreateCategoryHierarchyAsync();
        try
        {
            var response = await _client.PostAsJsonAsync(
                "/api/categories",
                new CreateCategoryRequest
                {
                    Id = GetUniqueTestId(),
                    Name = "L4_Invalid_" + Guid.NewGuid().ToString("N")[..8],
                    ParentId = l3.Id,
                    IsActive = true
                });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        finally
        {
            await DeleteCategorySafeAsync(l3.Id);
            await DeleteCategorySafeAsync(l2.Id);
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    [Fact]
    public async Task CreateCategory_NonexistentParent_ShouldReturn404NotFound()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/categories",
            new CreateCategoryRequest
            {
                Id = GetUniqueTestId(),
                Name = "Orphan_" + Guid.NewGuid().ToString("N")[..8],
                ParentId = 999999999L,
                IsActive = true
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_ChildUnderInactiveParent_ShouldReturn409Conflict()
    {
        var l1 = await CreateCategoryAsync(isActive: false);
        try
        {
            var response = await _client.PostAsJsonAsync(
                "/api/categories",
                new CreateCategoryRequest
                {
                    Id = GetUniqueTestId(),
                    Name = "Child_Of_Inactive_" + Guid.NewGuid().ToString("N")[..8],
                    ParentId = l1.Id,
                    IsActive = true
                });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        finally
        {
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    [Fact]
    public async Task CreateCategory_BlankOrWhitespaceName_ShouldReturn400BadRequest()
    {
        var emptyResponse = await _client.PostAsJsonAsync(
            "/api/categories",
            new CreateCategoryRequest
            {
                Id = GetUniqueTestId(),
                Name = "",
                ParentId = null,
                IsActive = true
            });
        Assert.Equal(HttpStatusCode.BadRequest, emptyResponse.StatusCode);

        var wsResponse = await _client.PostAsJsonAsync(
            "/api/categories",
            new CreateCategoryRequest
            {
                Id = GetUniqueTestId(),
                Name = "   ",
                ParentId = null,
                IsActive = true
            });
        Assert.Equal(HttpStatusCode.BadRequest, wsResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_WithChildCategories_ShouldReturn409Conflict()
    {
        var l1 = await CreateCategoryAsync();
        var l2 = await CreateCategoryAsync(parentId: l1.Id);
        try
        {
            var deleteResponse = await _client.DeleteAsync($"/api/categories/{l1.Id}");
            Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);
        }
        finally
        {
            await DeleteCategorySafeAsync(l2.Id);
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    [Fact]
    public async Task DeleteCategory_WithLinkedProducts_ShouldReturn409Conflict()
    {
        var (l1, l2, l3) = await CreateCategoryHierarchyAsync();
        long? prodId = null;
        try
        {
            var product = await CreateProductAsync(l3.Id);
            prodId = product.Id;

            var deleteResponse = await _client.DeleteAsync($"/api/categories/{l3.Id}");
            Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);
        }
        finally
        {
            if (prodId.HasValue) await HardDeleteProductAsync(prodId.Value);
            await DeleteCategorySafeAsync(l3.Id);
            await DeleteCategorySafeAsync(l2.Id);
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    [Fact]
    public async Task DeleteCategory_Nonexistent_ShouldReturn404NotFound()
    {
        var response = await _client.DeleteAsync("/api/categories/999999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCategories_ExcludesInactiveCategories_ByDefault()
    {
        var l1 = await CreateCategoryAsync(isActive: false);
        try
        {
            var response = await _client.GetAsync("/api/categories");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var categories = await response.Content.ReadFromJsonAsync<List<CategoryResponse>>();
            Assert.NotNull(categories);
            Assert.DoesNotContain(categories, c => c.Id == l1.Id);
        }
        finally
        {
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    [Fact]
    public async Task GetCategories_IncludesInactiveCategories_WhenIncludeInactiveIsTrue()
    {
        var l1 = await CreateCategoryAsync(isActive: false);
        try
        {
            var response = await _client.GetAsync("/api/categories?includeInactive=true");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var categories = await response.Content.ReadFromJsonAsync<List<CategoryResponse>>();
            Assert.NotNull(categories);
            var found = categories.FirstOrDefault(c => c.Id == l1.Id);
            Assert.NotNull(found);
            Assert.False(found.IsActive);
        }
        finally
        {
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    #endregion

    #region Product Integration Tests

    [Fact]
    public async Task CreateProduct_AssignedToLevel1Category_ShouldSucceed()
    {
        var l1 = await CreateCategoryAsync();
        var prodId = GetUniqueTestId();
        try
        {
            var response = await _client.PostAsJsonAsync(
                "/api/products",
                new CreateProductRequest
                {
                    Id = prodId,
                    Name = "Product L1 " + Guid.NewGuid().ToString("N")[..8],
                    Price = 15.00m,
                    StockQuantity = 10,
                    CategoryId = l1.Id
                });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var prod = await response.Content.ReadFromJsonAsync<ProductResponse>();
            Assert.NotNull(prod);
            Assert.Equal(l1.Id, prod.CategoryId);
            Assert.Equal((short)1, prod.CategoryLevel);
        }
        finally
        {
            await HardDeleteProductAsync(prodId);
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    [Fact]
    public async Task CreateProduct_AssignedToLevel2Category_ShouldSucceed()
    {
        var l1 = await CreateCategoryAsync();
        var l2 = await CreateCategoryAsync(parentId: l1.Id);
        var prodId = GetUniqueTestId();
        try
        {
            var response = await _client.PostAsJsonAsync(
                "/api/products",
                new CreateProductRequest
                {
                    Id = prodId,
                    Name = "Product L2 " + Guid.NewGuid().ToString("N")[..8],
                    Price = 15.00m,
                    StockQuantity = 10,
                    CategoryId = l2.Id
                });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var prod = await response.Content.ReadFromJsonAsync<ProductResponse>();
            Assert.NotNull(prod);
            Assert.Equal(l2.Id, prod.CategoryId);
            Assert.Equal((short)2, prod.CategoryLevel);
        }
        finally
        {
            await HardDeleteProductAsync(prodId);
            await DeleteCategorySafeAsync(l2.Id);
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    [Fact]
    public async Task CreateProduct_AssignedToActiveLevel3Category_ShouldSucceed()
    {
        var (l1, l2, l3) = await CreateCategoryHierarchyAsync();
        var prodId = GetUniqueTestId();
        try
        {
            var name = "Product L3 " + Guid.NewGuid().ToString("N")[..8];
            var response = await _client.PostAsJsonAsync(
                "/api/products",
                new CreateProductRequest
                {
                    Id = prodId,
                    Name = name,
                    Description = "Valid Level 3 product",
                    Price = 25.50m,
                    ImageUrl = "https://example.com/item.png",
                    StockQuantity = 30,
                    CategoryId = l3.Id
                });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
            Assert.NotNull(product);

            Assert.Equal(prodId, product.Id);
            Assert.Equal(name, product.Name);
            Assert.Equal("Valid Level 3 product", product.Description);
            Assert.Equal(25.50m, product.Price);
            Assert.Equal("https://example.com/item.png", product.ImageUrl);
            Assert.Equal(30, product.StockQuantity);
            Assert.Equal(l3.Id, product.CategoryId);
            Assert.Equal(l3.CategoryCode, product.CategoryCode);
            Assert.Equal(l3.Name, product.CategoryName);
            Assert.True(product.IsActive);

            var getResponse = await _client.GetAsync($"/api/products/{product.Id}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        }
        finally
        {
            await HardDeleteProductAsync(prodId);
            await DeleteCategorySafeAsync(l3.Id);
            await DeleteCategorySafeAsync(l2.Id);
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    [Fact]
    public async Task CreateProduct_NonexistentCategory_ShouldReturn404NotFound()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest
            {
                Id = GetUniqueTestId(),
                Name = "Product Orphan " + Guid.NewGuid().ToString("N")[..8],
                Price = 15.00m,
                StockQuantity = 10,
                CategoryId = 999999999L
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_AssignedToInactiveLevel3Category_ShouldReturn409Conflict()
    {
        var (l1, l2, l3) = await CreateCategoryHierarchyAsync(l3Active: false);
        try
        {
            var response = await _client.PostAsJsonAsync(
                "/api/products",
                new CreateProductRequest
                {
                    Id = GetUniqueTestId(),
                    Name = "Product Inactive L3 " + Guid.NewGuid().ToString("N")[..8],
                    Price = 15.00m,
                    StockQuantity = 10,
                    CategoryId = l3.Id
                });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        finally
        {
            await DeleteCategorySafeAsync(l3.Id);
            await DeleteCategorySafeAsync(l2.Id);
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    [Fact]
    public async Task CreateProduct_NegativePrice_ShouldReturn400BadRequest()
    {
        var (l1, l2, l3) = await CreateCategoryHierarchyAsync();
        try
        {
            var response = await _client.PostAsJsonAsync(
                "/api/products",
                new CreateProductRequest
                {
                    Id = GetUniqueTestId(),
                    Name = "Negative Price " + Guid.NewGuid().ToString("N")[..8],
                    Price = -10.00m,
                    StockQuantity = 5,
                    CategoryId = l3.Id
                });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await DeleteCategorySafeAsync(l3.Id);
            await DeleteCategorySafeAsync(l2.Id);
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    [Fact]
    public async Task CreateProduct_NegativeStockQuantity_ShouldReturn400BadRequest()
    {
        var (l1, l2, l3) = await CreateCategoryHierarchyAsync();
        try
        {
            var response = await _client.PostAsJsonAsync(
                "/api/products",
                new CreateProductRequest
                {
                    Id = GetUniqueTestId(),
                    Name = "Negative Stock " + Guid.NewGuid().ToString("N")[..8],
                    Price = 10.00m,
                    StockQuantity = -5,
                    CategoryId = l3.Id
                });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await DeleteCategorySafeAsync(l3.Id);
            await DeleteCategorySafeAsync(l2.Id);
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    [Fact]
    public async Task UpdateProduct_ValidLevel3Category_ShouldSucceed()
    {
        var (l1, l2, l3A) = await CreateCategoryHierarchyAsync();
        var l3B = await CreateCategoryAsync(parentId: l2.Id);
        long? prodId = null;
        try
        {
            var product = await CreateProductAsync(l3A.Id);
            prodId = product.Id;

            var updatedName = "Updated Prod " + Guid.NewGuid().ToString("N")[..8];
            var updateResponse = await _client.PutAsJsonAsync(
                $"/api/products/{product.Id}",
                new UpdateProductRequest
                {
                    Name = updatedName,
                    Description = "Updated description",
                    Price = 99.99m,
                    ImageUrl = "https://example.com/updated.png",
                    StockQuantity = 75,
                    CategoryId = l3B.Id,
                    IsActive = true
                });

            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            var updated = await updateResponse.Content.ReadFromJsonAsync<ProductResponse>();
            Assert.NotNull(updated);
            Assert.Equal(product.Id, updated.Id);
            Assert.Equal(updatedName, updated.Name);
            Assert.Equal("Updated description", updated.Description);
            Assert.Equal(99.99m, updated.Price);
            Assert.Equal("https://example.com/updated.png", updated.ImageUrl);
            Assert.Equal(75, updated.StockQuantity);
            Assert.Equal(l3B.Id, updated.CategoryId);
            Assert.Equal(l3B.CategoryCode, updated.CategoryCode);
            Assert.Equal(l3B.Name, updated.CategoryName);
            Assert.True(updated.IsActive);
        }
        finally
        {
            if (prodId.HasValue) await HardDeleteProductAsync(prodId.Value);
            await DeleteCategorySafeAsync(l3B.Id);
            await DeleteCategorySafeAsync(l3A.Id);
            await DeleteCategorySafeAsync(l2.Id);
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    [Fact]
    public async Task UpdateProduct_ToLevel1OrLevel2Category_ShouldSucceed()
    {
        var (l1, l2, l3) = await CreateCategoryHierarchyAsync();
        long? prodId = null;
        try
        {
            var product = await CreateProductAsync(l3.Id);
            prodId = product.Id;

            var updateToL1 = await _client.PutAsJsonAsync(
                $"/api/products/{product.Id}",
                new UpdateProductRequest
                {
                    Name = product.Name,
                    Price = product.Price,
                    StockQuantity = product.StockQuantity,
                    CategoryId = l1.Id,
                    IsActive = true
                });
            Assert.Equal(HttpStatusCode.OK, updateToL1.StatusCode);
            var updated1 = await updateToL1.Content.ReadFromJsonAsync<ProductResponse>();
            Assert.Equal(l1.Id, updated1!.CategoryId);
            Assert.Equal((short)1, updated1.CategoryLevel);

            var updateToL2 = await _client.PutAsJsonAsync(
                $"/api/products/{product.Id}",
                new UpdateProductRequest
                {
                    Name = product.Name,
                    Price = product.Price,
                    StockQuantity = product.StockQuantity,
                    CategoryId = l2.Id,
                    IsActive = true
                });
            Assert.Equal(HttpStatusCode.OK, updateToL2.StatusCode);
            var updated2 = await updateToL2.Content.ReadFromJsonAsync<ProductResponse>();
            Assert.Equal(l2.Id, updated2!.CategoryId);
            Assert.Equal((short)2, updated2.CategoryLevel);
        }
        finally
        {
            if (prodId.HasValue) await HardDeleteProductAsync(prodId.Value);
            await DeleteCategorySafeAsync(l3.Id);
            await DeleteCategorySafeAsync(l2.Id);
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    [Fact]
    public async Task UploadProductImage_ValidFile_ShouldSucceedAndReturnPath()
    {
        using var content = new MultipartFormDataContent();
        var fileBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG magic bytes
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test_item.png");

        var response = await _client.PostAsync("/api/products/upload-image", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ImageUploadResponse>();
        Assert.NotNull(result);
        Assert.StartsWith("/uploads/products/", result.ImageUrl);
        Assert.EndsWith(".png", result.ImageUrl);
    }

    [Fact]
    public async Task UploadProductImage_InvalidExtension_ShouldReturn400BadRequest()
    {
        using var content = new MultipartFormDataContent();
        var fileBytes = System.Text.Encoding.UTF8.GetBytes("not an image");
        var fileContent = new ByteArrayContent(fileBytes);
        content.Add(fileContent, "file", "bad_script.bat");

        var response = await _client.PostAsync("/api/products/upload-image", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_PerformsSoftDelete_RowRemainsAndIsActiveFalse()
    {
        var (l1, l2, l3) = await CreateCategoryHierarchyAsync();
        long? prodId = null;
        try
        {
            var product = await CreateProductAsync(l3.Id);
            prodId = product.Id;

            var deleteResponse = await _client.DeleteAsync($"/api/products/{product.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            // API GET by ID still returns the product with IsActive = false
            var getResponse = await _client.GetAsync($"/api/products/{product.Id}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var apiProduct = await getResponse.Content.ReadFromJsonAsync<ProductResponse>();
            Assert.NotNull(apiProduct);
            Assert.False(apiProduct.IsActive);

            // Directly verify in the PostgreSQL database that the row is still present
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbProduct = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
            Assert.NotNull(dbProduct);
            Assert.False(dbProduct.IsActive);
        }
        finally
        {
            if (prodId.HasValue) await HardDeleteProductAsync(prodId.Value);
            await DeleteCategorySafeAsync(l3.Id);
            await DeleteCategorySafeAsync(l2.Id);
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    [Fact]
    public async Task GetProducts_ExcludesInactiveProducts_ByDefault()
    {
        var (l1, l2, l3) = await CreateCategoryHierarchyAsync();
        long? prodId = null;
        try
        {
            var product = await CreateProductAsync(l3.Id);
            prodId = product.Id;

            var deleteResponse = await _client.DeleteAsync($"/api/products/{product.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var listResponse = await _client.GetAsync($"/api/products?categoryCode={l3.CategoryCode}");
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
            var products = await listResponse.Content.ReadFromJsonAsync<List<ProductResponse>>();
            Assert.NotNull(products);
            Assert.DoesNotContain(products, p => p.Id == product.Id);
        }
        finally
        {
            if (prodId.HasValue) await HardDeleteProductAsync(prodId.Value);
            await DeleteCategorySafeAsync(l3.Id);
            await DeleteCategorySafeAsync(l2.Id);
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    [Fact]
    public async Task GetProducts_IncludesInactiveProducts_WhenIncludeInactiveIsTrue()
    {
        var (l1, l2, l3) = await CreateCategoryHierarchyAsync();
        long? prodId = null;
        try
        {
            var product = await CreateProductAsync(l3.Id);
            prodId = product.Id;

            var deleteResponse = await _client.DeleteAsync($"/api/products/{product.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var listResponse = await _client.GetAsync($"/api/products?categoryCode={l3.CategoryCode}&includeInactive=true");
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
            var products = await listResponse.Content.ReadFromJsonAsync<List<ProductResponse>>();
            Assert.NotNull(products);
            var found = products.FirstOrDefault(p => p.Id == product.Id);
            Assert.NotNull(found);
            Assert.False(found.IsActive);
        }
        finally
        {
            if (prodId.HasValue) await HardDeleteProductAsync(prodId.Value);
            await DeleteCategorySafeAsync(l3.Id);
            await DeleteCategorySafeAsync(l2.Id);
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    [Fact]
    public async Task GetProducts_PaginationAndCategoryCodeFilter_WorkCorrectly()
    {
        var (l1, l2, l3A) = await CreateCategoryHierarchyAsync();
        var l3B = await CreateCategoryAsync(parentId: l2.Id);
        var productIds = new List<long>();
        try
        {
            var prodA1 = await CreateProductAsync(l3A.Id);
            productIds.Add(prodA1.Id);
            var prodA2 = await CreateProductAsync(l3A.Id);
            productIds.Add(prodA2.Id);
            var prodA3 = await CreateProductAsync(l3A.Id);
            productIds.Add(prodA3.Id);

            var prodB1 = await CreateProductAsync(l3B.Id);
            productIds.Add(prodB1.Id);

            // Filter by category code: should only return L3A products
            var filterResponse = await _client.GetAsync($"/api/products?categoryCode={l3A.CategoryCode}&pageSize=50");
            Assert.Equal(HttpStatusCode.OK, filterResponse.StatusCode);
            var filteredProducts = await filterResponse.Content.ReadFromJsonAsync<List<ProductResponse>>();
            Assert.NotNull(filteredProducts);
            Assert.All(filteredProducts, p => Assert.Equal(l3A.CategoryCode, p.CategoryCode));
            Assert.Contains(filteredProducts, p => p.Id == prodA1.Id);
            Assert.Contains(filteredProducts, p => p.Id == prodA2.Id);
            Assert.Contains(filteredProducts, p => p.Id == prodA3.Id);
            Assert.DoesNotContain(filteredProducts, p => p.Id == prodB1.Id);

            // Pagination on L3A products: page 1 with pageSize = 2
            var page1Response = await _client.GetAsync($"/api/products?categoryCode={l3A.CategoryCode}&page=1&pageSize=2");
            Assert.Equal(HttpStatusCode.OK, page1Response.StatusCode);
            var page1 = await page1Response.Content.ReadFromJsonAsync<List<ProductResponse>>();
            Assert.NotNull(page1);
            Assert.Equal(2, page1.Count);

            // Pagination on L3A products: page 2 with pageSize = 2
            var page2Response = await _client.GetAsync($"/api/products?categoryCode={l3A.CategoryCode}&page=2&pageSize=2");
            Assert.Equal(HttpStatusCode.OK, page2Response.StatusCode);
            var page2 = await page2Response.Content.ReadFromJsonAsync<List<ProductResponse>>();
            Assert.NotNull(page2);
            Assert.Single(page2);

            // Check no overlap between page 1 and page 2
            var page1Ids = page1.Select(p => p.Id).ToHashSet();
            Assert.DoesNotContain(page2, p => page1Ids.Contains(p.Id));
        }
        finally
        {
            foreach (var pid in productIds)
            {
                await HardDeleteProductAsync(pid);
            }
            await DeleteCategorySafeAsync(l3B.Id);
            await DeleteCategorySafeAsync(l3A.Id);
            await DeleteCategorySafeAsync(l2.Id);
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    #endregion

    #region Manual ID Entry Tests

    [Fact]
    public async Task CreateCategory_ValidManualId_ShouldSucceedAndPreserveId()
    {
        var manualId = GetUniqueTestId();
        var name = "ManualCat_" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var response = await _client.PostAsJsonAsync(
                "/api/categories",
                new CreateCategoryRequest
                {
                    Id = manualId,
                    Name = name,
                    ParentId = null,
                    IsActive = true
                });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var category = await response.Content.ReadFromJsonAsync<CategoryResponse>();
            Assert.NotNull(category);
            Assert.Equal(manualId, category.Id);
            Assert.Equal(name, category.Name);
            Assert.Matches(@"^\d{5}$", category.CategoryCode);
        }
        finally
        {
            await DeleteCategorySafeAsync(manualId);
        }
    }

    [Fact]
    public async Task CreateCategory_MissingOrInvalidId_ShouldReturn400BadRequest()
    {
        // Zero ID
        var zeroIdResponse = await _client.PostAsJsonAsync(
            "/api/categories",
            new CreateCategoryRequest
            {
                Id = 0,
                Name = "Zero ID " + Guid.NewGuid().ToString("N")[..8],
                ParentId = null,
                IsActive = true
            });
        Assert.Equal(HttpStatusCode.BadRequest, zeroIdResponse.StatusCode);

        // Negative ID
        var negIdResponse = await _client.PostAsJsonAsync(
            "/api/categories",
            new CreateCategoryRequest
            {
                Id = -10,
                Name = "Negative ID " + Guid.NewGuid().ToString("N")[..8],
                ParentId = null,
                IsActive = true
            });
        Assert.Equal(HttpStatusCode.BadRequest, negIdResponse.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_DuplicateId_ShouldReturn409Conflict()
    {
        var dupId = GetUniqueTestId();
        var first = await CreateCategoryAsync(id: dupId);
        try
        {
            var response = await _client.PostAsJsonAsync(
                "/api/categories",
                new CreateCategoryRequest
                {
                    Id = dupId,
                    Name = "Duplicate ID " + Guid.NewGuid().ToString("N")[..8],
                    ParentId = null,
                    IsActive = true
                });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        finally
        {
            await DeleteCategorySafeAsync(dupId);
        }
    }

    [Fact]
    public async Task CreateProduct_ValidManualId_ShouldSucceedAndPreserveId()
    {
        var (l1, l2, l3) = await CreateCategoryHierarchyAsync();
        var manualProdId = GetUniqueTestId();
        try
        {
            var name = "ManualProd_" + Guid.NewGuid().ToString("N")[..8];
            var response = await _client.PostAsJsonAsync(
                "/api/products",
                new CreateProductRequest
                {
                    Id = manualProdId,
                    Name = name,
                    Price = 59.99m,
                    StockQuantity = 20,
                    CategoryId = l3.Id
                });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
            Assert.NotNull(product);
            Assert.Equal(manualProdId, product.Id);
            Assert.Equal(name, product.Name);
            Assert.Equal(l3.Id, product.CategoryId);
        }
        finally
        {
            await HardDeleteProductAsync(manualProdId);
            await DeleteCategorySafeAsync(l3.Id);
            await DeleteCategorySafeAsync(l2.Id);
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    [Fact]
    public async Task CreateProduct_MissingOrInvalidId_ShouldReturn400BadRequest()
    {
        var (l1, l2, l3) = await CreateCategoryHierarchyAsync();
        try
        {
            // Zero ID
            var zeroResponse = await _client.PostAsJsonAsync(
                "/api/products",
                new CreateProductRequest
                {
                    Id = 0,
                    Name = "Zero Prod ID",
                    Price = 10.00m,
                    StockQuantity = 5,
                    CategoryId = l3.Id
                });
            Assert.Equal(HttpStatusCode.BadRequest, zeroResponse.StatusCode);

            // Negative ID
            var negResponse = await _client.PostAsJsonAsync(
                "/api/products",
                new CreateProductRequest
                {
                    Id = -5,
                    Name = "Neg Prod ID",
                    Price = 10.00m,
                    StockQuantity = 5,
                    CategoryId = l3.Id
                });
            Assert.Equal(HttpStatusCode.BadRequest, negResponse.StatusCode);
        }
        finally
        {
            await DeleteCategorySafeAsync(l3.Id);
            await DeleteCategorySafeAsync(l2.Id);
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    [Fact]
    public async Task CreateProduct_DuplicateId_ShouldReturn409Conflict()
    {
        var (l1, l2, l3) = await CreateCategoryHierarchyAsync();
        var dupProdId = GetUniqueTestId();
        try
        {
            var product = await CreateProductAsync(l3.Id, id: dupProdId);

            var duplicateResponse = await _client.PostAsJsonAsync(
                "/api/products",
                new CreateProductRequest
                {
                    Id = dupProdId,
                    Name = "Duplicate Product",
                    Price = 20.00m,
                    StockQuantity = 10,
                    CategoryId = l3.Id
                });

            Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        }
        finally
        {
            await HardDeleteProductAsync(dupProdId);
            await DeleteCategorySafeAsync(l3.Id);
            await DeleteCategorySafeAsync(l2.Id);
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    [Fact]
    public async Task CreateCategoryHierarchy_WithManualIds_ShouldLinkCorrectly()
    {
        var l1Id = GetUniqueTestId();
        var l2Id = GetUniqueTestId();
        var l3Id = GetUniqueTestId();
        try
        {
            var l1 = await CreateCategoryAsync(id: l1Id);
            var l2 = await CreateCategoryAsync(id: l2Id, parentId: l1.Id);
            var l3 = await CreateCategoryAsync(id: l3Id, parentId: l2.Id);

            Assert.Equal(l1Id, l1.Id);
            Assert.Null(l1.ParentId);
            Assert.Equal((short)1, l1.Level);

            Assert.Equal(l2Id, l2.Id);
            Assert.Equal(l1Id, l2.ParentId);
            Assert.Equal(l1.CategoryCode, l2.ParentCategoryCode);
            Assert.Equal((short)2, l2.Level);

            Assert.Equal(l3Id, l3.Id);
            Assert.Equal(l2Id, l3.ParentId);
            Assert.Equal(l2.CategoryCode, l3.ParentCategoryCode);
            Assert.Equal((short)3, l3.Level);
        }
        finally
        {
            await DeleteCategorySafeAsync(l3Id);
            await DeleteCategorySafeAsync(l2Id);
            await DeleteCategorySafeAsync(l1Id);
        }
    }

    [Fact]
    public async Task CreateProduct_WithManualIdAndManualCategoryId_ShouldLinkCorrectly()
    {
        var (l1, l2, l3) = await CreateCategoryHierarchyAsync();
        var manualProdId = GetUniqueTestId();
        try
        {
            var product = await CreateProductAsync(l3.Id, id: manualProdId);

            Assert.Equal(manualProdId, product.Id);
            Assert.Equal(l3.Id, product.CategoryId);
            Assert.Equal(l3.CategoryCode, product.CategoryCode);
            Assert.Equal(l3.Name, product.CategoryName);

            var getResponse = await _client.GetAsync($"/api/products/{manualProdId}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var fetched = await getResponse.Content.ReadFromJsonAsync<ProductResponse>();
            Assert.NotNull(fetched);
            Assert.Equal(manualProdId, fetched.Id);
            Assert.Equal(l3.Id, fetched.CategoryId);
            Assert.Equal(l3.CategoryCode, fetched.CategoryCode);
        }
        finally
        {
            await HardDeleteProductAsync(manualProdId);
            await DeleteCategorySafeAsync(l3.Id);
            await DeleteCategorySafeAsync(l2.Id);
            await DeleteCategorySafeAsync(l1.Id);
        }
    }

    #endregion

    #region Helpers

    private async Task<CategoryResponse> CreateCategoryAsync(
        long? id = null,
        string? name = null,
        long? parentId = null,
        bool isActive = true)
    {
        var categoryId = id ?? GetUniqueTestId();
        var categoryName = name ?? ("TestCat_" + Guid.NewGuid().ToString("N")[..8]);
        var response = await _client.PostAsJsonAsync(
            "/api/categories",
            new CreateCategoryRequest
            {
                Id = categoryId,
                Name = categoryName,
                ParentId = parentId,
                IsActive = isActive
            });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(result);
        return result;
    }

    private async Task<(CategoryResponse l1, CategoryResponse l2, CategoryResponse l3)> CreateCategoryHierarchyAsync(
        long? l1Id = null,
        long? l2Id = null,
        long? l3Id = null,
        bool l1Active = true,
        bool l2Active = true,
        bool l3Active = true)
    {
        var l1 = await CreateCategoryAsync(id: l1Id, isActive: l1Active);
        var l2 = await CreateCategoryAsync(id: l2Id, parentId: l1.Id, isActive: l2Active);
        var l3 = await CreateCategoryAsync(id: l3Id, parentId: l2.Id, isActive: l3Active);
        return (l1, l2, l3);
    }

    private async Task<ProductResponse> CreateProductAsync(
        long categoryId,
        long? id = null,
        string? name = null,
        decimal price = 29.99m,
        int stockQuantity = 10,
        string? description = "Test Product Description",
        string? imageUrl = "https://example.com/test.png")
    {
        var productId = id ?? GetUniqueTestId();
        var productName = name ?? ("TestProd_" + Guid.NewGuid().ToString("N")[..8]);
        var response = await _client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest
            {
                Id = productId,
                Name = productName,
                Description = description,
                Price = price,
                ImageUrl = imageUrl,
                StockQuantity = stockQuantity,
                CategoryId = categoryId
            });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(result);
        return result;
    }

    private async Task DeleteCategorySafeAsync(long categoryId)
    {
        try
        {
            var resp = await _client.DeleteAsync($"/api/categories/{categoryId}");
            if (!resp.IsSuccessStatusCode)
            {
                await HardDeleteCategoryAsync(categoryId);
            }
        }
        catch
        {
            await HardDeleteCategoryAsync(categoryId);
        }
    }

    private async Task HardDeleteCategoryAsync(long categoryId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var category = await db.Categories.FindAsync(categoryId);
        if (category != null)
        {
            db.Categories.Remove(category);
            await db.SaveChangesAsync();
        }
    }

    private async Task HardDeleteProductAsync(long productId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Products.FindAsync(productId);
        if (product != null)
        {
            db.Products.Remove(product);
            await db.SaveChangesAsync();
        }
    }

    #endregion
}