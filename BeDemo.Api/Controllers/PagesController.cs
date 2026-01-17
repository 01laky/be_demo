using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PagesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PagesController> _logger;

    public PagesController(
        ApplicationDbContext context,
        ILogger<PagesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/pages?faceId={faceId}
    /// Get list of pages for a specific face
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPages([FromQuery] int? faceId)
    {
        try
        {
            IQueryable<Page> query = _context.Pages;

            if (faceId.HasValue)
            {
                query = query.Where(p => p.FaceId == faceId.Value);
            }

            var pages = await query
                .OrderBy(p => p.Index)
                .ThenBy(p => p.Name)
                .ToListAsync();

            var pageDtos = pages.Select(p => new
            {
                id = p.Id,
                faceId = p.FaceId,
                pageTypeId = p.PageTypeId,
                name = p.Name,
                description = p.Description,
                path = p.Path,
                index = p.Index,
                createdAt = p.CreatedAt,
                updatedAt = p.UpdatedAt,
            }).ToList();

            _logger.LogInformation("Retrieved {Count} pages", pageDtos.Count);
            return Ok(pageDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pages");
            return StatusCode(500, new { error = "An error occurred while retrieving pages" });
        }
    }

    /// <summary>
    /// GET /api/pages/{id}
    /// Get page by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetPage(int id)
    {
        try
        {
            var page = await _context.Pages.FindAsync(id);

            if (page == null)
            {
                _logger.LogWarning("Page not found: {PageId}", id);
                return NotFound(new { error = "Page not found" });
            }

            var pageDto = new
            {
                id = page.Id,
                faceId = page.FaceId,
                pageTypeId = page.PageTypeId,
                name = page.Name,
                description = page.Description,
                path = page.Path,
                index = page.Index,
                createdAt = page.CreatedAt,
                updatedAt = page.UpdatedAt,
            };

            _logger.LogInformation("Retrieved page: {PageId}", id);
            return Ok(pageDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving page: {PageId}", id);
            return StatusCode(500, new { error = "An error occurred while retrieving page" });
        }
    }

    /// <summary>
    /// POST /api/pages
    /// Create a new page
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreatePage([FromBody] CreatePageModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            // Verify that Face exists
            var faceExists = await _context.Faces.AnyAsync(f => f.Id == model.FaceId);
            if (!faceExists)
            {
                _logger.LogWarning("Face not found: {FaceId}", model.FaceId);
                return BadRequest(new { error = "Face not found" });
            }

            // Verify that PageType exists
            var pageTypeExists = await _context.PageTypes.AnyAsync(pt => pt.Id == model.PageTypeId);
            if (!pageTypeExists)
            {
                _logger.LogWarning("PageType not found: {PageTypeId}", model.PageTypeId);
                return BadRequest(new { error = "PageType not found" });
            }

            var page = new Page
            {
                FaceId = model.FaceId,
                PageTypeId = model.PageTypeId,
                Name = model.Name,
                Description = model.Description,
                Path = model.Path,
                Index = model.Index,
                CreatedAt = DateTime.UtcNow,
            };

            _context.Pages.Add(page);
            await _context.SaveChangesAsync();

            var pageDto = new
            {
                id = page.Id,
                faceId = page.FaceId,
                pageTypeId = page.PageTypeId,
                name = page.Name,
                description = page.Description,
                path = page.Path,
                index = page.Index,
                createdAt = page.CreatedAt,
                updatedAt = page.UpdatedAt,
            };

            _logger.LogInformation("Page created: {PageId}", page.Id);
            return CreatedAtAction(nameof(GetPage), new { id = page.Id }, pageDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating page");
            return StatusCode(500, new { error = "An error occurred while creating page" });
        }
    }

    /// <summary>
    /// PUT /api/pages/{id}
    /// Update page by ID
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePage(int id, [FromBody] UpdatePageModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var page = await _context.Pages.FindAsync(id);

            if (page == null)
            {
                _logger.LogWarning("Page not found for update: {PageId}", id);
                return NotFound(new { error = "Page not found" });
            }

            // Update page properties
            if (model.Name != null)
            {
                page.Name = model.Name;
            }
            if (model.Description != null)
            {
                page.Description = model.Description;
            }
            if (model.Path != null)
            {
                page.Path = model.Path;
            }
            if (model.Index.HasValue)
            {
                page.Index = model.Index.Value;
            }
            if (model.FaceId.HasValue)
            {
                // Verify that new Face exists
                var faceExists = await _context.Faces.AnyAsync(f => f.Id == model.FaceId.Value);
                if (!faceExists)
                {
                    _logger.LogWarning("Face not found: {FaceId}", model.FaceId.Value);
                    return BadRequest(new { error = "Face not found" });
                }
                page.FaceId = model.FaceId.Value;
            }
            if (model.PageTypeId.HasValue)
            {
                // Verify that new PageType exists
                var pageTypeExists = await _context.PageTypes.AnyAsync(pt => pt.Id == model.PageTypeId.Value);
                if (!pageTypeExists)
                {
                    _logger.LogWarning("PageType not found: {PageTypeId}", model.PageTypeId.Value);
                    return BadRequest(new { error = "PageType not found" });
                }
                page.PageTypeId = model.PageTypeId.Value;
            }
            page.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var pageDto = new
            {
                id = page.Id,
                faceId = page.FaceId,
                pageTypeId = page.PageTypeId,
                name = page.Name,
                description = page.Description,
                path = page.Path,
                index = page.Index,
                createdAt = page.CreatedAt,
                updatedAt = page.UpdatedAt,
            };

            _logger.LogInformation("Page updated: {PageId}", id);
            return Ok(pageDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating page: {PageId}", id);
            return StatusCode(500, new { error = "An error occurred while updating page" });
        }
    }

    /// <summary>
    /// DELETE /api/pages/{id}
    /// Delete page by ID
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePage(int id)
    {
        try
        {
            var page = await _context.Pages.FindAsync(id);

            if (page == null)
            {
                _logger.LogWarning("Page not found for deletion: {PageId}", id);
                return NotFound(new { error = "Page not found" });
            }

            _context.Pages.Remove(page);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Page deleted: {PageId}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting page: {PageId}", id);
            return StatusCode(500, new { error = "An error occurred while deleting page" });
        }
    }
}

/// <summary>
/// Model for creating a new page
/// </summary>
public class CreatePageModel
{
    [Required(ErrorMessage = "FaceId is required")]
    public int FaceId { get; set; }

    [Required(ErrorMessage = "PageTypeId is required")]
    public int PageTypeId { get; set; }

    [Required(ErrorMessage = "Name is required")]
    [StringLength(200, ErrorMessage = "Name must be at most 200 characters")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description must be at most 1000 characters")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Path is required")]
    [StringLength(500, ErrorMessage = "Path must be at most 500 characters")]
    public string Path { get; set; } = string.Empty;

    public int Index { get; set; } = 0;
}

/// <summary>
/// Model for updating a page
/// </summary>
public class UpdatePageModel
{
    public int? FaceId { get; set; }

    public int? PageTypeId { get; set; }

    [StringLength(200, ErrorMessage = "Name must be at most 200 characters")]
    public string? Name { get; set; }

    [StringLength(1000, ErrorMessage = "Description must be at most 1000 characters")]
    public string? Description { get; set; }

    [StringLength(500, ErrorMessage = "Path must be at most 500 characters")]
    public string? Path { get; set; }

    public int? Index { get; set; }
}
