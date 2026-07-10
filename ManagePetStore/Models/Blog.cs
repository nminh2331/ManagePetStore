using System;
using System.Collections.Generic;

namespace ManagePetStore.Models;

public partial class Blog
{
    public int BlogId { get; set; }

    public string Title { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string ContentBody { get; set; } = null!;

    public int AuthorId { get; set; }

    public string? CoverImage { get; set; }

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? Category { get; set; }

    public bool IsFeatured { get; set; }

    public int ViewCount { get; set; }

    public virtual User Author { get; set; } = null!;
}
