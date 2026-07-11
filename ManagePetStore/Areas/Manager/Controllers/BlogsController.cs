using ManagePetStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace ManagePetStore.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Roles = "manager,admin")]
public class BlogsController : Controller
{
    private readonly PetStoreManagementContext _context;
    private readonly ILogger<BlogsController> _logger;
    private readonly IWebHostEnvironment _env;

    // Thư mục lưu ảnh bìa bài viết
    private const string UploadSubFolder = "uploads/blogs";

    public BlogsController(
        PetStoreManagementContext context,
        ILogger<BlogsController> logger,
        IWebHostEnvironment env)
    {
        _context = context;
        _logger = logger;
        _env = env;
    }

    // =========================================================================
    // INDEX - Danh sách bài viết
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, string? search = null)
    {
        const int pageSize = 10;

        try
        {
            var query = _context.Blogs
                .Include(b => b.Author)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(b =>
                    b.Title.Contains(term) ||
                    (b.Category != null && b.Category.Contains(term)));
            }

            var total = await query.CountAsync();
            var blogs = await query
                .OrderByDescending(b => b.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.Search = search;
            ViewBag.TotalCount = total;

            return View(blogs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải danh sách bài viết Blog.");
            TempData["Error"] = "Không thể tải danh sách bài viết. Vui lòng thử lại.";
            return View(new List<Blog>());
        }
    }

    // =========================================================================
    // CREATE GET
    // =========================================================================
    [HttpGet]
    public IActionResult Create()
    {
        return View(new Blog { CreatedAt = DateTime.Now });
    }

    // =========================================================================
    // CREATE POST
    // =========================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Blog blog, IFormFile? coverImageFile)
    {
        // Gán AuthorId từ user đang đăng nhập
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
        {
            TempData["Error"] = "Không xác định được tài khoản. Vui lòng đăng nhập lại.";
            return View(blog);
        }

        blog.AuthorId = userId;

        // Tự động sinh Slug nếu để trống
        if (string.IsNullOrWhiteSpace(blog.Slug))
        {
            blog.Slug = GenerateSlug(blog.Title);
        }
        else
        {
            blog.Slug = GenerateSlug(blog.Slug);
        }

        blog.CreatedAt = DateTime.Now;
        blog.ViewCount = 0;

        // Kiểm tra Slug trùng lặp
        try
        {
            var slugExists = await _context.Blogs
                .AnyAsync(b => b.Slug == blog.Slug);
            if (slugExists)
            {
                ModelState.AddModelError("Slug", $"Slug '{blog.Slug}' đã tồn tại. Vui lòng dùng slug khác.");
                return View(blog);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi kiểm tra slug trùng lặp.");
        }

        ModelState.Remove("Author");
        ModelState.Remove("CoverImage");
        ModelState.Remove("Slug");

        if (!ModelState.IsValid)
        {
            return View(blog);
        }

        // Xử lý upload ảnh bìa
        if (coverImageFile != null && coverImageFile.Length > 0)
        {
            var uploadResult = await SaveCoverImageAsync(coverImageFile);
            if (uploadResult.Success)
            {
                blog.CoverImage = uploadResult.RelativePath;
            }
            else
            {
                ModelState.AddModelError("CoverImage", uploadResult.ErrorMessage ?? "Không thể upload ảnh.");
                return View(blog);
            }
        }

        try
        {
            _context.Blogs.Add(blog);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã tạo bài viết \"{blog.Title}\" thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Lỗi Database khi tạo bài viết Blog.");

            // Kiểm tra lỗi Unique Constraint (Slug trùng)
            if (dbEx.InnerException?.Message.Contains("UQ_Blogs_Slug") == true ||
                dbEx.InnerException?.Message.Contains("Slug") == true)
            {
                ModelState.AddModelError("Slug", "Slug này đã tồn tại trong hệ thống. Vui lòng thay đổi.");
            }
            else
            {
                ModelState.AddModelError("", "Lỗi cơ sở dữ liệu khi lưu bài viết. Vui lòng thử lại.");
            }
            return View(blog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không xác định khi tạo bài viết Blog.");
            ModelState.AddModelError("", "Đã xảy ra lỗi không mong muốn. Vui lòng thử lại.");
            return View(blog);
        }
    }

    // =========================================================================
    // EDIT GET
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var blog = await _context.Blogs
                .Include(b => b.Author)
                .FirstOrDefaultAsync(b => b.BlogId == id);

            if (blog == null)
            {
                TempData["Error"] = "Không tìm thấy bài viết.";
                return RedirectToAction(nameof(Index));
            }

            return View(blog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải bài viết BlogId={BlogId} để chỉnh sửa.", id);
            TempData["Error"] = "Không thể tải bài viết. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
    }

    // =========================================================================
    // EDIT POST
    // =========================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Blog blog, IFormFile? coverImageFile)
    {
        if (id != blog.BlogId)
        {
            return BadRequest("ID không khớp.");
        }

        try
        {
            var existing = await _context.Blogs.FindAsync(id);
            if (existing == null)
            {
                TempData["Error"] = "Không tìm thấy bài viết.";
                return RedirectToAction(nameof(Index));
            }

            // Kiểm tra Slug trùng (ngoại trừ chính bài này)
            if (!string.IsNullOrWhiteSpace(blog.Slug))
            {
                var newSlug = GenerateSlug(blog.Slug);
                var slugConflict = await _context.Blogs
                    .AnyAsync(b => b.Slug == newSlug && b.BlogId != id);
                if (slugConflict)
                {
                    ModelState.AddModelError("Slug", $"Slug '{newSlug}' đã tồn tại. Vui lòng dùng slug khác.");
                    blog.CoverImage = existing.CoverImage;
                    return View(blog);
                }
                existing.Slug = newSlug;
            }

            // Cập nhật các trường
            existing.Title = blog.Title;
            existing.ContentBody = blog.ContentBody;
            existing.Category = blog.Category;
            existing.IsFeatured = blog.IsFeatured;
            existing.IsPublished = blog.IsPublished;

            // Xử lý upload ảnh mới (nếu có)
            if (coverImageFile != null && coverImageFile.Length > 0)
            {
                var uploadResult = await SaveCoverImageAsync(coverImageFile);
                if (uploadResult.Success)
                {
                    // Xóa ảnh cũ nếu tồn tại
                    DeleteOldCoverImage(existing.CoverImage);
                    existing.CoverImage = uploadResult.RelativePath;
                }
                else
                {
                    ModelState.AddModelError("CoverImage", uploadResult.ErrorMessage ?? "Không thể upload ảnh.");
                    blog.CoverImage = existing.CoverImage;
                    return View(blog);
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã cập nhật bài viết \"{existing.Title}\" thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _context.Blogs.AnyAsync(b => b.BlogId == id))
            {
                TempData["Error"] = "Bài viết không còn tồn tại.";
                return RedirectToAction(nameof(Index));
            }
            _logger.LogWarning("Concurrency conflict khi cập nhật BlogId={BlogId}.", id);
            ModelState.AddModelError("", "Xung đột dữ liệu. Vui lòng tải lại và thử lại.");
            return View(blog);
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Lỗi Database khi cập nhật BlogId={BlogId}.", id);
            if (dbEx.InnerException?.Message.Contains("UQ_Blogs_Slug") == true)
            {
                ModelState.AddModelError("Slug", "Slug này đã tồn tại trong hệ thống.");
            }
            else
            {
                ModelState.AddModelError("", "Lỗi cơ sở dữ liệu khi lưu bài viết. Vui lòng thử lại.");
            }
            return View(blog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không xác định khi cập nhật BlogId={BlogId}.", id);
            ModelState.AddModelError("", "Đã xảy ra lỗi không mong muốn. Vui lòng thử lại.");
            return View(blog);
        }
    }

    // =========================================================================
    // DELETE POST
    // =========================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var blog = await _context.Blogs.FindAsync(id);
            if (blog == null)
            {
                TempData["Error"] = "Không tìm thấy bài viết.";
                return RedirectToAction(nameof(Index));
            }

            var title = blog.Title;
            _context.Blogs.Remove(blog);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã xóa bài viết \"{title}\".";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xóa BlogId={BlogId}.", id);
            TempData["Error"] = "Không thể xóa bài viết. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
    }

    // =========================================================================
    // TOGGLE PUBLISH POST
    // =========================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublish(int id)
    {
        try
        {
            var blog = await _context.Blogs.FindAsync(id);
            if (blog == null)
            {
                return Json(new { success = false, message = "Không tìm thấy bài viết." });
            }

            blog.IsPublished = !blog.IsPublished;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                isPublished = blog.IsPublished,
                message = blog.IsPublished ? "Đã đăng bài thành công." : "Đã gỡ bài viết."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi toggle publish BlogId={BlogId}.", id);
            return Json(new { success = false, message = "Lỗi hệ thống. Vui lòng thử lại." });
        }
    }

    // =========================================================================
    // UPLOAD IMAGE - Dành cho CKEditor 5 SimpleUploadAdapter
    // CKEditor POST với field "upload", nhận response JSON { "url": "..." }
    // =========================================================================
    [HttpPost]
    [IgnoreAntiforgeryToken] // CKEditor gửi token qua header, không qua form field
    public async Task<IActionResult> UploadImage(IFormFile upload)
    {
        if (upload == null || upload.Length == 0)
        {
            return BadRequest(new { error = new { message = "Không nhận được file ảnh." } });
        }

        try
        {
            // Kiểm tra định dạng
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".avif" };
            var ext = Path.GetExtension(upload.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                return BadRequest(new
                {
                    error = new { message = "Chỉ chấp nhận ảnh: JPG, PNG, GIF, WEBP, AVIF." }
                });
            }

            // Giới hạn 8MB cho ảnh nội dung
            if (upload.Length > 8 * 1024 * 1024)
            {
                return BadRequest(new
                {
                    error = new { message = "Ảnh không được vượt quá 8MB." }
                });
            }

            // Lưu vào wwwroot/uploads/blogs/content/
            const string contentSubFolder = "uploads/blogs/content";
            var uploadDir = Path.Combine(_env.WebRootPath, contentSubFolder);

            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            var fileName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(uploadDir, fileName);

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await upload.CopyToAsync(stream);

            // Trả về URL theo chuẩn CKEditor SimpleUploadAdapter
            var url = $"/{contentSubFolder}/{fileName}";
            return Ok(new { url });
        }
        catch (IOException ioEx)
        {
            _logger.LogError(ioEx, "Lỗi I/O khi upload ảnh nội dung Blog.");
            return StatusCode(500, new { error = new { message = "Lỗi hệ thống khi lưu ảnh. Vui lòng thử lại." } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không xác định khi upload ảnh nội dung Blog.");
            return StatusCode(500, new { error = new { message = "Upload thất bại. Vui lòng thử lại." } });
        }
    }

    // =========================================================================
    // PRIVATE HELPERS
    // =========================================================================

    /// <summary>Upload ảnh bìa vào wwwroot/uploads/blogs/ và trả về đường dẫn tương đối.</summary>
    private async Task<(bool Success, string? RelativePath, string? ErrorMessage)> SaveCoverImageAsync(IFormFile file)
    {
        try
        {
            // Kiểm tra định dạng file
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                return (false, null, "Chỉ chấp nhận file ảnh: JPG, JPEG, PNG, GIF, WEBP.");
            }

            // Giới hạn kích thước 5MB
            if (file.Length > 5 * 1024 * 1024)
            {
                return (false, null, "Ảnh bìa không được vượt quá 5MB.");
            }

            var uploadDir = Path.Combine(_env.WebRootPath, UploadSubFolder);

            // Tạo thư mục nếu chưa tồn tại
            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            // Tên file unique để tránh ghi đè
            var fileName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(uploadDir, fileName);

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            // Đường dẫn tương đối để lưu vào DB (luôn dùng forward slash)
            var relativePath = $"/{UploadSubFolder}/{fileName}";
            return (true, relativePath, null);
        }
        catch (IOException ioEx)
        {
            _logger.LogError(ioEx, "Lỗi I/O khi lưu file ảnh bìa Blog.");
            return (false, null, "Lỗi hệ thống khi lưu file ảnh. Vui lòng thử lại.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không xác định khi upload ảnh bìa Blog.");
            return (false, null, "Không thể upload ảnh. Vui lòng thử lại.");
        }
    }

    /// <summary>Xóa ảnh bìa cũ khỏi wwwroot nếu tồn tại.</summary>
    private void DeleteOldCoverImage(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;

        try
        {
            // Chuyển đường dẫn tương đối sang đường dẫn vật lý
            var physicalPath = Path.Combine(_env.WebRootPath, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(physicalPath))
            {
                System.IO.File.Delete(physicalPath);
            }
        }
        catch (IOException ioEx)
        {
            _logger.LogWarning(ioEx, "Không thể xóa ảnh bìa cũ: {Path}", relativePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi xóa ảnh bìa cũ: {Path}", relativePath);
        }
    }

    /// <summary>Chuyển chuỗi thành Slug hợp lệ (ASCII, lowercase, chỉ có dấu gạch ngang).</summary>
    private static string GenerateSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return Guid.NewGuid().ToString("N")[..8];

        // Chuẩn hóa Unicode sang dạng tương đương ASCII
        var normalized = input.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in normalized)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var slug = sb.ToString().Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant();

        // Xử lý các ký tự tiếng Việt đặc biệt còn sót
        slug = slug
            .Replace("đ", "d").Replace("Đ", "d")
            .Replace("ă", "a").Replace("â", "a")
            .Replace("ê", "e").Replace("ô", "o")
            .Replace("ơ", "o").Replace("ư", "u");

        // Thay ký tự không phải chữ/số bằng dấu gạch ngang
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-{2,}", "-");
        slug = slug.Trim('-');

        // Giới hạn độ dài slug
        if (slug.Length > 200) slug = slug[..200].TrimEnd('-');

        return string.IsNullOrEmpty(slug) ? Guid.NewGuid().ToString("N")[..8] : slug;
    }
}
