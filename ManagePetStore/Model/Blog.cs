using System;
using System.Collections.Generic;

namespace ManagePetStore.Model;

public partial class Blog
{
    public int BlogId { get; set; }

    public string Title { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Summary { get; set; }

    public string ContentBody { get; set; } = null!;

    public int AuthorId { get; set; }

    public string? CoverImage { get; set; }

    public string? MetaTitle { get; set; }

    public string? MetaDescription { get; set; }

    public bool? IsPublished { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User Author { get; set; } = null!;
}
