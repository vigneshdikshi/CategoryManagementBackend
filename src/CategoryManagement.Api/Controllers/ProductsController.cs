using CategoryManagement.Api.DTOs;
using CategoryManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CategoryManagement.Api.Controllers;

[ApiController]
[Route("api/products")]
[Produces("application/json")]
public class ProductsController(IProductService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetAll(
        [FromQuery] string? categoryCode,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) return BadRequest(new { message = "page must be greater than or equal to 1." });
        if (pageSize is < 1 or > 100) return BadRequest(new { message = "pageSize must be between 1 and 100." });

        return Ok(await service.GetAllAsync(categoryCode, includeInactive, page, pageSize, ct));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ProductResponse>> GetById(long id, CancellationToken ct)
    {
        var product = await service.GetByIdAsync(id, ct);
        return product is null ? NotFound(new { message = $"Product {id} was not found." }) : Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create([FromBody] CreateProductRequest request, CancellationToken ct)
    {
        try
        {
            var created = await service.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ProductResponse>> Update(long id, [FromBody] UpdateProductRequest request, CancellationToken ct)
    {
        try
        {
            var updated = await service.UpdateAsync(id, request, ct);
            return updated is null ? NotFound(new { message = $"Product {id} was not found." }) : Ok(updated);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var deleted = await service.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound(new { message = $"Product {id} was not found." });
    }

    [HttpPost("upload-image")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ImageUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImageUploadResponse>> UploadImage(
        IFormFile? file,
        [FromServices] IWebHostEnvironment env,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "No image file was uploaded." });
        }

        // Limit size to 5 MB
        const long maxBytes = 5 * 1024 * 1024;
        if (file.Length > maxBytes)
        {
            return BadRequest(new { message = "Image file size exceeds the 5 MB limit." });
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".svg" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
        {
            return BadRequest(new { message = $"Unsupported image format. Allowed formats: {string.Join(", ", allowedExtensions)}" });
        }

        var rootPath = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var uploadDir = Path.Combine(rootPath, "uploads", "products");
        if (!Directory.Exists(uploadDir))
        {
            Directory.CreateDirectory(uploadDir);
        }

        var uniqueFileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadDir, uniqueFileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream, ct);
        }

        var relativeUrl = $"/uploads/products/{uniqueFileName}";
        return Ok(new ImageUploadResponse(relativeUrl, file.FileName, file.Length));
    }
}
