using System.Net;
using System.Net.Http.Json;
using CategoryManagement.Api;
using CategoryManagement.Api.Data;
using CategoryManagement.Api.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CategoryManagement.Tests;

public class DevelopmentApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((context, config) =>
        {
            var conn = context.Configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(conn) ||
                conn.Contains("Database=CategoryManagementDB_Test", StringComparison.OrdinalIgnoreCase))
            {
                var targetConn = (conn ?? "Server=.;Database=DIKSHIKART;Integrated Security=True;TrustServerCertificate=True;")
                    .Replace("Database=CategoryManagementDB_Test", "Database=DIKSHIKART", StringComparison.OrdinalIgnoreCase);

                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = targetConn
                });
            }
        });
    }
}

public class DataRetrievalRegressionTests : IClassFixture<DevelopmentApiFactory>
{
    private readonly HttpClient _client;
    private readonly DevelopmentApiFactory _factory;

    public DataRetrievalRegressionTests(DevelopmentApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost:5080"),
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public void DatabaseConfiguration_ShouldPointToCategoryManagementDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        Assert.NotNull(connectionString);
        Assert.True(
            connectionString.Contains("Database=DIKSHIKART", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("Database=CategoryManagementDB", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Database=CategoryManagementDB_Test", connectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturn200AndConnected()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"database\":\"connected\"", content);
    }

    [Fact]
    public async Task GetAllCategories_IncludeInactive_ShouldReturnAll6Records()
    {
        var response = await _client.GetAsync("/api/categories?includeInactive=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var categories = await response.Content.ReadFromJsonAsync<List<CategoryResponse>>();
        Assert.NotNull(categories);
        Assert.Equal(6, categories.Count);

        var electronics = categories.SingleOrDefault(c => c.Id == 1001);
        Assert.NotNull(electronics);
        Assert.Equal("00083", electronics.CategoryCode);
        Assert.Equal("Electronics", electronics.Name);
        Assert.Null(electronics.ParentId);
        Assert.Equal((short)1, electronics.Level);
        Assert.True(electronics.IsActive);

        var mobilePhones = categories.SingleOrDefault(c => c.Id == 1002);
        Assert.NotNull(mobilePhones);
        Assert.Equal("00084", mobilePhones.CategoryCode);
        Assert.Equal("Mobile Phones", mobilePhones.Name);
        Assert.Equal(1001, mobilePhones.ParentId);
        Assert.Equal((short)2, mobilePhones.Level);
        Assert.True(mobilePhones.IsActive);

        var androidPhones = categories.SingleOrDefault(c => c.Id == 1003);
        Assert.NotNull(androidPhones);
        Assert.Equal("00085", androidPhones.CategoryCode);
        Assert.Equal("Android Phones", androidPhones.Name);
        Assert.Equal(1002, androidPhones.ParentId);
        Assert.Equal((short)3, androidPhones.Level);
        Assert.True(androidPhones.IsActive);

        var fashion = categories.SingleOrDefault(c => c.Id == 2001);
        Assert.NotNull(fashion);
        Assert.Equal("00086", fashion.CategoryCode);
        Assert.Equal("Fashion", fashion.Name);
        Assert.Null(fashion.ParentId);
        Assert.Equal((short)1, fashion.Level);
        Assert.True(fashion.IsActive);

        var mensClothing = categories.SingleOrDefault(c => c.Id == 2002);
        Assert.NotNull(mensClothing);
        Assert.Equal("00087", mensClothing.CategoryCode);
        Assert.Equal("Men's Clothing", mensClothing.Name);
        Assert.Equal(2001, mensClothing.ParentId);
        Assert.Equal((short)2, mensClothing.Level);
        Assert.True(mensClothing.IsActive);

        var tShirts = categories.SingleOrDefault(c => c.Id == 2003);
        Assert.NotNull(tShirts);
        Assert.Equal("00088", tShirts.CategoryCode);
        Assert.Equal("T-Shirts", tShirts.Name);
        Assert.Equal(2002, tShirts.ParentId);
        Assert.Equal((short)3, tShirts.Level);
        Assert.True(tShirts.IsActive);
    }

    [Fact]
    public async Task GetAllCategories_DefaultActiveOnly_ShouldReturnActiveCategories()
    {
        var response = await _client.GetAsync("/api/categories");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var categories = await response.Content.ReadFromJsonAsync<List<CategoryResponse>>();
        Assert.NotNull(categories);
        Assert.Equal(6, categories.Count);
        Assert.All(categories, c => Assert.True(c.IsActive));
    }

    [Fact]
    public async Task GetCategoryById_Existing1001_ShouldReturnElectronics()
    {
        var response = await _client.GetAsync("/api/categories/1001");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var category = await response.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(category);
        Assert.Equal(1001, category.Id);
        Assert.Equal("00083", category.CategoryCode);
        Assert.Equal("Electronics", category.Name);
    }

    [Fact]
    public async Task GetCategoryByCode_Existing00083_ShouldReturnElectronics()
    {
        var response = await _client.GetAsync("/api/categories/code/00083");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var category = await response.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(category);
        Assert.Equal(1001, category.Id);
        Assert.Equal("Electronics", category.Name);
    }

    [Fact]
    public async Task GetProducts_IncludeInactive_ShouldReturnSamsungGalaxyS25()
    {
        var response = await _client.GetAsync("/api/products?includeInactive=true&page=1&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var products = await response.Content.ReadFromJsonAsync<List<ProductResponse>>();
        Assert.NotNull(products);
        Assert.Single(products);

        var product = products[0];
        Assert.Equal(5001, product.Id);
        Assert.Equal("Samsung Galaxy S25", product.Name);
        Assert.Equal(1003, product.CategoryId);
        Assert.Equal("00085", product.CategoryCode);
        Assert.Equal("Android Phones", product.CategoryName);
        Assert.True(product.IsActive);
    }

    [Fact]
    public async Task GetProducts_DefaultActive_ShouldReturnSamsungGalaxyS25()
    {
        var response = await _client.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var products = await response.Content.ReadFromJsonAsync<List<ProductResponse>>();
        Assert.NotNull(products);
        Assert.Single(products);
        Assert.Equal(5001, products[0].Id);
    }

    [Fact]
    public async Task GetProductById_Existing5001_ShouldReturnSamsungGalaxyS25()
    {
        var response = await _client.GetAsync("/api/products/5001");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(product);
        Assert.Equal(5001, product.Id);
        Assert.Equal("Samsung Galaxy S25", product.Name);
        Assert.Equal(1003, product.CategoryId);
    }

    [Fact]
    public async Task Cors_PreflightRequestFromReactOrigin_ShouldIncludeCorsHeaders()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/categories");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "accept");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").First());
    }

    [Fact]
    public async Task Cors_GetRequestFromReactOrigin_ShouldIncludeCorsHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/categories?includeInactive=true");
        request.Headers.Add("Origin", "http://localhost:5173");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").First());
    }
}
