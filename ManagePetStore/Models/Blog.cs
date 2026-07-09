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

    /// <summary>Thể loại bài viết (vd: Dinh dưỡng, Sức khỏe, ...)</summary>
    public string? Category { get; set; }

    /// <summary>Bài viết nổi bật — ưu tiên hiển thị lên đầu trang chủ.</summary>
    public bool IsFeatured { get; set; }

    /// <summary>Lượt xem — tăng +1 mỗi khi khách truy cập chi tiết bài viết.</summary>
    public int ViewCount { get; set; }

    public virtual User Author { get; set; } = null!;
}
