using System.ComponentModel.DataAnnotations;

namespace BeDemo.Api.Models;

/// <summary>
/// Face entity model
/// </summary>
public class Face
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Index { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(50)]
    public string? Color { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation property - one Face has many Pages
    public ICollection<Page> Pages { get; set; } = new List<Page>();
}
