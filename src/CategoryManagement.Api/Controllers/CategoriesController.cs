using CategoryManagement.Api.DTOs;
using CategoryManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CategoryManagement.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Produces("application/json")]
public class CategoriesController(ICategoryService service) : ControllerBase
{
    /// <summary>Get categories. Use parentId to fetch a parent's direct children, or level=1 for root categories.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetAll(
        [FromQuery] long? parentId,
        [FromQuery] short? level,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        if (level is < 1 or > 3)
            return BadRequest(new { message = "level must be 1, 2, or 3." });

        var categories = await service.GetAllAsync(parentId, level, includeInactive, ct);
        return Ok(categories);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<CategoryResponse>> GetById(long id, CancellationToken ct)
    {
        var category = await service.GetByIdAsync(id, ct);
        return category is null ? NotFound(new { message = $"Category {id} was not found." }) : Ok(category);
    }

    [HttpGet("code/{code}")]
    public async Task<ActionResult<CategoryResponse>> GetByCode(string code, CancellationToken ct)
    {
        var category = await service.GetByCodeAsync(code, ct);
        return category is null ? NotFound(new { message = $"Category code {code} was not found." }) : Ok(category);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create([FromBody] CreateCategoryRequest request, CancellationToken ct)
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
    public async Task<ActionResult<CategoryResponse>> Update(long id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
    {
        try
        {
            var updated = await service.UpdateAsync(id, request, ct);
            return updated is null ? NotFound(new { message = $"Category {id} was not found." }) : Ok(updated);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        try
        {
            var deleted = await service.DeleteAsync(id, ct);
            return deleted ? NoContent() : NotFound(new { message = $"Category {id} was not found." });
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
