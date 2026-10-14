using System.Text.Json.Serialization;
using CategoryManagement.Api.Data;
using CategoryManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Category Management API",
        Version = "v1",
        Description = "Three-level category and product management API for a React Native app."
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is missing. Configure it in appsettings.json or ConnectionStrings__DefaultConnection.");
}

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var resolvedConnectionString = config.GetConnectionString("DefaultConnection") ?? connectionString;
    options.UseSqlServer(resolvedConnectionString);
});

builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentClients", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Category Management API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseCors("DevelopmentClients");

var uploadsDir = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "uploads", "products");
if (!Directory.Exists(uploadsDir))
{
    Directory.CreateDirectory(uploadsDir);
}

app.UseStaticFiles();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    service = "Category Management API",
    status = "running",
    swagger = "/swagger",
    health = "/api/health"
}));

app.MapGet("/api/health", async (AppDbContext db, CancellationToken cancellationToken) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? Results.Ok(new { status = "ok", database = "connected" })
            : Results.Json(new { status = "error", database = "unavailable" }, statusCode: 503);
    }
    catch
    {
        return Results.Json(new { status = "error", database = "unavailable" }, statusCode: 503);
    }
});

app.Run();
public partial class Program { }
