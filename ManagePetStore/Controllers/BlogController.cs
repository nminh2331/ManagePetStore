using ManagePetStore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Controllers;

/// <summary>
/// Controller công khai cho phép khách hàng đọc bài viết Blog.
/// Route: /Blog/Details/{slug}
/// </summary>
public class BlogController : Controller
{
    private readonly PetStoreManagementContext _context;
    private readonly ILogger<BlogController> _logger;

    public BlogController(PetStoreManagementContext context, ILogger<BlogController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // =========================================================================
    // DETAILS - Xem chi tiết bài viết theo Slug
    // Đồng thời tăng ViewCount +1 mỗi lần truy cập
    // =========================================================================
    [HttpGet]
    [Route("Blog/{slug}")]
    public async Task<IActionResult> Details(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        Blog? blog = null;

        try
        {
            blog = await _context.Blogs
                .Include(b => b.Author)
                .FirstOrDefaultAsync(b => b.Slug == slug && b.IsPublished);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi Database khi tìm bài viết với slug='{Slug}'.", slug);
            return StatusCode(500, "Lỗi hệ thống. Vui lòng thử lại sau.");
        }

        if (blog == null)
        {
            return NotFound();
        }

        // Tăng ViewCount +1 (cách ly trong try-catch riêng để không làm hỏng trang)
        try
        {
            blog.ViewCount += 1;
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Lỗi đếm view không nên làm gián đoạn trải nghiệm đọc bài
            _logger.LogWarning(ex, "Không thể cập nhật ViewCount cho BlogId={BlogId}.", blog.BlogId);
        }

        // Lấy các bài liên quan cùng thể loại (ngoại trừ bài hiện tại)
        var relatedBlogs = new List<Blog>();
        try
        {
            relatedBlogs = await _context.Blogs
                .Where(b => b.IsPublished &&
                            b.BlogId != blog.BlogId &&
                            b.Category == blog.Category)
                .OrderByDescending(b => b.ViewCount)
                .Take(3)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể tải bài viết liên quan cho BlogId={BlogId}.", blog.BlogId);
        }

        ViewBag.RelatedBlogs = relatedBlogs;
        return View(blog);
    }

    // =========================================================================
    // INDEX - Danh sách tất cả bài viết (trang /Blog)
    // =========================================================================
    [HttpGet]
    [Route("Blog")]
    public async Task<IActionResult> Index(int page = 1, string? category = null, string? search = null)
    {
        const int pageSize = 9;

        try
        {
            var query = _context.Blogs
                .Include(b => b.Author)
                .Where(b => b.IsPublished)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(b => b.Category == category);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(b => b.Title.Contains(term));
            }

            var total = await query.CountAsync();
            var blogs = await query
                .OrderByDescending(b => b.IsFeatured)
                .ThenByDescending(b => b.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Lấy danh sách thể loại để hiển thị bộ lọc
            var categories = await _context.Blogs
                .Where(b => b.IsPublished && b.Category != null)
                .Select(b => b.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.TotalCount = total;
            ViewBag.SelectedCategory = category;
            ViewBag.SearchKeyword = search;
            ViewBag.Categories = categories;

            return View(blogs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải danh sách Blog công khai.");
            return View(new List<Blog>());
        }
    }
}
