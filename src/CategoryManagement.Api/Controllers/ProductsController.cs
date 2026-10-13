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
}
