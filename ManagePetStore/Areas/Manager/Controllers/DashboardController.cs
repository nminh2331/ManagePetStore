using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ManagePetStore.Areas.Manager.Models;
using ManagePetStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Roles = "manager,admin")]
public class DashboardController : Controller
{
    private readonly PetStoreManagementContext _context;

    public DashboardController(PetStoreManagementContext context)
    {
        _context = context;
    }

    private static readonly string[] CompletedStatuses = new[] { "completed", "hoàn thành", "hoan thanh", "đã hoàn thành" };

    // =========================================================================
    // INDEX - Trang Dashboard chính
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate)
    {
        ViewData["ManagerNav"] = "dashboard";

        // Mặc định 30 ngày qua nếu không truyền ngày
        if (!fromDate.HasValue)
        {
            fromDate = DateTime.Today.AddDays(-29);
        }
        if (!toDate.HasValue)
        {
            toDate = DateTime.Today;
        }

        var startDateTime = fromDate.Value.Date;
        var endDateTime = toDate.Value.Date.AddDays(1).AddTicks(-1);

        // 1. Lọc đơn hàng đã hoàn thành trong kỳ (online & quầy)
        var completedOrders = await _context.Orders
            .Where(o => o.Date >= startDateTime && o.Date <= endDateTime)
            .Where(o => 
                (o.OrderId.StartsWith("OD-") && (o.Status == "Chờ xử lý" || o.Status == "Đã thanh toán" || o.Status == "da thanh toan")) ||
                (o.OrderId.StartsWith("ORD-") && (o.Status == "Completed" || o.Status == "Đã hoàn thành" || o.Status == "hoan thanh" || o.Status == "completed"))
            )
            .ToListAsync();

        decimal totalRevenue = completedOrders.Sum(o => o.Total);
        int totalOrdersCount = completedOrders.Count;

        // 2. Lọc dòng tiền ra (nhập kho hoàn thành trong kỳ)
        var completedImports = await _context.StockMovements
            .Where(m => m.Date >= startDateTime && m.Date <= endDateTime)
            .Where(m => m.Type == "Nhập hàng" && m.Status == "Hoàn thành")
            .ToListAsync();

        decimal cashOut = completedImports.Sum(m => m.TotalValue);

        // 3. Dòng tiền vào = Doanh thu từ đơn hàng hoàn thành
        decimal cashIn = totalRevenue;
        decimal netCashFlow = cashIn - cashOut;

        // 4. Tổng lượt xem blog
        int totalBlogViews = await _context.Blogs.SumAsync(b => b.ViewCount);

        var viewModel = new DashboardSummaryViewModel
        {
            TotalRevenue = totalRevenue,
            CashIn = cashIn,
            CashOut = cashOut,
            NetCashFlow = netCashFlow,
            TotalOrders = totalOrdersCount,
            TotalBlogViews = totalBlogViews,
            FromDate = fromDate.Value,
            ToDate = toDate.Value
        };

        ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");

        return View(viewModel);
    }

    // =========================================================================
    // API: Lấy dữ liệu vẽ các biểu đồ
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> GetChartData(string? fromDate, string? toDate)
    {
        DateTime start = string.IsNullOrEmpty(fromDate) 
            ? DateTime.Today.AddDays(-29) 
            : DateTime.ParseExact(fromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            
        DateTime end = string.IsNullOrEmpty(toDate) 
            ? DateTime.Today 
            : DateTime.ParseExact(toDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        var startDateTime = start.Date;
        var endDateTime = end.Date.AddDays(1).AddTicks(-1);

        var diffDays = (end - start).TotalDays;

        // --- 1. Dữ liệu biểu đồ xu hướng (Doanh thu & Dòng tiền ra) ---
        var chartPoints = new List<ChartDataPoint>();

        var orders = await _context.Orders
            .Where(o => o.Date >= startDateTime && o.Date <= endDateTime)
            .Where(o => 
                (o.OrderId.StartsWith("OD-") && (o.Status == "Chờ xử lý" || o.Status == "Đã thanh toán" || o.Status == "da thanh toan")) ||
                (o.OrderId.StartsWith("ORD-") && (o.Status == "Completed" || o.Status == "Đã hoàn thành" || o.Status == "hoan thanh" || o.Status == "completed"))
            )
            .Select(o => new { o.Date, o.Total })
            .ToListAsync();

        var imports = await _context.StockMovements
            .Where(m => m.Date >= startDateTime && m.Date <= endDateTime)
            .Where(m => m.Type == "Nhập hàng" && m.Status == "Hoàn thành")
            .Select(m => new { m.Date, m.TotalValue })
            .ToListAsync();

        if (diffDays <= 31)
        {
            // Nhóm theo ngày
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
            {
                var label = d.ToString("dd/MM");
                var dailyRevenue = orders.Where(o => o.Date.Date == d).Sum(o => o.Total);
                var dailyImport = imports.Where(m => m.Date.Date == d).Sum(m => m.TotalValue);

                chartPoints.Add(new ChartDataPoint
                {
                    Label = label,
                    Value = dailyRevenue,
                    Value2 = dailyImport
                });
            }
        }
        else
        {
            // Nhóm theo tháng
            var current = new DateTime(start.Year, start.Month, 1);
            var last = new DateTime(end.Year, end.Month, 1);

            while (current <= last)
            {
                var label = current.ToString("MM/yyyy");
                var monthlyRevenue = orders.Where(o => o.Date.Year == current.Year && o.Date.Month == current.Month).Sum(o => o.Total);
                var monthlyImport = imports.Where(m => m.Date.Year == current.Year && m.Date.Month == current.Month).Sum(m => m.TotalValue);

                chartPoints.Add(new ChartDataPoint
                {
                    Label = label,
                    Value = monthlyRevenue,
                    Value2 = monthlyImport
                });

                current = current.AddMonths(1);
            }
        }

        // --- 2. Dữ liệu biểu đồ cơ cấu Doanh thu (Sản phẩm, Spa, Khách sạn) ---
        var orderItems = await _context.OrderItems
            .Include(oi => oi.Order)
            .Include(oi => oi.SpaService)
            .Include(oi => oi.RoomType)
            .Where(oi => oi.Order.Date >= startDateTime && oi.Order.Date <= endDateTime)
            .Where(oi => 
                (oi.Order.OrderId.StartsWith("OD-") && (oi.Order.Status == "Chờ xử lý" || oi.Order.Status == "Đã thanh toán" || oi.Order.Status == "da thanh toan")) ||
                (oi.Order.OrderId.StartsWith("ORD-") && (oi.Order.Status == "Completed" || oi.Order.Status == "Đã hoàn thành" || oi.Order.Status == "hoan thanh" || oi.Order.Status == "completed"))
            )
            .ToListAsync();

        decimal productRevenue = orderItems.Where(oi => oi.ProductSku != null).Sum(oi => oi.Price * oi.Quantity);
        decimal spaRevenue = orderItems.Where(oi => oi.SpaServiceId != null).Sum(oi => oi.Price * oi.Quantity);
        decimal hotelRevenue = orderItems.Where(oi => oi.RoomTypeId != null).Sum(oi => oi.Price * oi.Quantity);

        // --- 3. Dịch vụ Spa hot nhất ---
        var topSpaServices = orderItems
            .Where(oi => oi.SpaServiceId != null && oi.SpaService != null)
            .GroupBy(oi => new { oi.SpaServiceId, oi.SpaService!.Name })
            .Select(g => new TopServiceItem
            {
                ServiceId = g.Key.SpaServiceId ?? 0,
                Name = g.Key.Name,
                Type = "Spa",
                UsageCount = g.Sum(oi => oi.Quantity),
                TotalRevenue = g.Sum(oi => oi.Price * oi.Quantity)
            })
            .OrderByDescending(x => x.UsageCount)
            .Take(5)
            .ToList();

        // --- 4. Dịch vụ Phòng Khách sạn hot nhất ---
        var topHotelRooms = orderItems
            .Where(oi => oi.RoomTypeId != null && oi.RoomType != null)
            .GroupBy(oi => new { oi.RoomTypeId, oi.RoomType!.Type })
            .Select(g => new TopServiceItem
            {
                ServiceId = g.Key.RoomTypeId ?? 0,
                Name = g.Key.Type,
                Type = "Chuồng",
                UsageCount = g.Sum(oi => oi.Quantity),
                TotalRevenue = g.Sum(oi => oi.Price * oi.Quantity)
            })
            .OrderByDescending(x => x.UsageCount)
            .Take(5)
            .ToList();

        // --- 5. Blog đọc nhiều nhất ---
        var topBlogs = await _context.Blogs
            .Include(b => b.Author)
            .OrderByDescending(b => b.ViewCount)
            .Take(5)
            .Select(b => new TopBlogItem
            {
                BlogId = b.BlogId,
                Title = b.Title,
                AuthorName = b.Author != null ? b.Author.FullName : "Admin",
                Category = b.Category ?? "Chung",
                ViewCount = b.ViewCount,
                CreatedAt = b.CreatedAt
            })
            .ToListAsync();

        return Json(new
        {
            Trend = chartPoints,
            Structure = new { Product = productRevenue, Spa = spaRevenue, Hotel = hotelRevenue },
            TopSpa = topSpaServices,
            TopHotel = topHotelRooms,
            TopBlogs = topBlogs
        });
    }

    // =========================================================================
    // AJAX DRILL-DOWN: Chi tiết doanh thu theo danh mục (Sản phẩm/Spa/Khách sạn)
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> GetRevenueDetails(string category, string? fromDate, string? toDate)
    {
        DateTime start = string.IsNullOrEmpty(fromDate) 
            ? DateTime.Today.AddDays(-29) 
            : DateTime.ParseExact(fromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            
        DateTime end = string.IsNullOrEmpty(toDate) 
            ? DateTime.Today 
            : DateTime.ParseExact(toDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        var startDateTime = start.Date;
        var endDateTime = end.Date.AddDays(1).AddTicks(-1);

        var orders = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
            .Where(o => o.Date >= startDateTime && o.Date <= endDateTime)
            .Where(o => 
                (o.OrderId.StartsWith("OD-") && (o.Status == "Chờ xử lý" || o.Status == "Đã thanh toán" || o.Status == "da thanh toan")) ||
                (o.OrderId.StartsWith("ORD-") && (o.Status == "Completed" || o.Status == "Đã hoàn thành" || o.Status == "hoan thanh" || o.Status == "completed"))
            )
            .ToListAsync();

        var filtered = new List<RevenueDetailItem>();

        foreach (var o in orders)
        {
            bool hasItem = false;
            if (category.Equals("Product", StringComparison.OrdinalIgnoreCase))
            {
                hasItem = o.OrderItems.Any(oi => oi.ProductSku != null);
            }
            else if (category.Equals("Spa", StringComparison.OrdinalIgnoreCase))
            {
                hasItem = o.OrderItems.Any(oi => oi.SpaServiceId != null);
            }
            else if (category.Equals("Hotel", StringComparison.OrdinalIgnoreCase))
            {
                hasItem = o.OrderItems.Any(oi => oi.RoomTypeId != null);
            }

            if (hasItem)
            {
                filtered.Add(new RevenueDetailItem
                {
                    OrderId = o.OrderId,
                    CustomerName = o.Customer?.FullName ?? "Khách vãng lai",
                    OrderDate = o.Date,
                    Total = o.Total,
                    PaymentMethod = o.PaymentMethod,
                    Status = o.Status
                });
            }
        }

        return Json(filtered.OrderByDescending(f => f.OrderDate));
    }

    // =========================================================================
    // AJAX DRILL-DOWN: Chi tiết doanh thu theo ngày cụ thể
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> GetRevenueByDateDetails(string dateStr)
    {
        // dateStr dạng "dd/MM" hoặc "MM/yyyy"
        DateTime targetDateStart;
        DateTime targetDateEnd;

        if (dateStr.Contains("/"))
        {
            var parts = dateStr.Split('/');
            if (parts[1].Length == 4) // Định dạng MM/yyyy
            {
                int month = int.Parse(parts[0]);
                int year = int.Parse(parts[1]);
                targetDateStart = new DateTime(year, month, 1);
                targetDateEnd = targetDateStart.AddMonths(1).AddTicks(-1);
            }
            else // Định dạng dd/MM (mặc định lấy năm hiện tại)
            {
                int day = int.Parse(parts[0]);
                int month = int.Parse(parts[1]);
                int year = DateTime.Today.Year;
                targetDateStart = new DateTime(year, month, day);
                targetDateEnd = targetDateStart.AddDays(1).AddTicks(-1);
            }
        }
        else
        {
            return BadRequest("Định dạng ngày không hợp lệ.");
        }

        var orders = await _context.Orders
            .Include(o => o.Customer)
            .Where(o => o.Date >= targetDateStart && o.Date <= targetDateEnd)
            .Where(o => 
                (o.OrderId.StartsWith("OD-") && (o.Status == "Chờ xử lý" || o.Status == "Đã thanh toán" || o.Status == "da thanh toan")) ||
                (o.OrderId.StartsWith("ORD-") && (o.Status == "Completed" || o.Status == "Đã hoàn thành" || o.Status == "hoan thanh" || o.Status == "completed"))
            )
            .Select(o => new RevenueDetailItem
            {
                OrderId = o.OrderId,
                CustomerName = o.Customer != null ? o.Customer.FullName : "Khách vãng lai",
                OrderDate = o.Date,
                Total = o.Total,
                PaymentMethod = o.PaymentMethod,
                Status = o.Status
            })
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        return Json(orders);
    }

    // =========================================================================
    // AJAX DRILL-DOWN: Chi tiết dòng tiền vào/ra
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> GetCashFlowDetails(string type, string? fromDate, string? toDate)
    {
        DateTime start = string.IsNullOrEmpty(fromDate) 
            ? DateTime.Today.AddDays(-29) 
            : DateTime.ParseExact(fromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            
        DateTime end = string.IsNullOrEmpty(toDate) 
            ? DateTime.Today 
            : DateTime.ParseExact(toDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        var startDateTime = start.Date;
        var endDateTime = end.Date.AddDays(1).AddTicks(-1);

        var details = new List<CashFlowDetailItem>();

        if (type.Equals("in", StringComparison.OrdinalIgnoreCase))
        {
            // Dòng tiền vào từ các đơn hàng đã hoàn thành
            var orders = await _context.Orders
                .Include(o => o.Customer)
                .Where(o => o.Date >= startDateTime && o.Date <= endDateTime)
                .Where(o => 
                    (o.OrderId.StartsWith("OD-") && (o.Status == "Chờ xử lý" || o.Status == "Đã thanh toán" || o.Status == "da thanh toan")) ||
                    (o.OrderId.StartsWith("ORD-") && (o.Status == "Completed" || o.Status == "Đã hoàn thành" || o.Status == "hoan thanh" || o.Status == "completed"))
                )
                .ToListAsync();

            details.AddRange(orders.Select(o => new CashFlowDetailItem
            {
                ReferenceId = o.OrderId,
                Type = "Tiền vào (In)",
                Source = "Đơn hàng",
                Date = o.Date,
                Amount = o.Total,
                Description = $"Thanh toán đơn hàng từ KH: {o.Customer?.FullName ?? "Khách vãng lai"} ({o.PaymentMethod})"
            }));
        }
        else if (type.Equals("out", StringComparison.OrdinalIgnoreCase))
        {
            // Dòng tiền ra từ phiếu nhập hàng đã hoàn thành
            var movements = await _context.StockMovements
                .Include(m => m.CreatedBy)
                .Include(m => m.SupplierNavigation)
                .Where(m => m.Date >= startDateTime && m.Date <= endDateTime)
                .Where(m => m.Type == "Nhập hàng" && m.Status == "Hoàn thành")
                .ToListAsync();

            details.AddRange(movements.Select(m => new CashFlowDetailItem
            {
                ReferenceId = m.MovementId.ToString(),
                Type = "Tiền ra (Out)",
                Source = "Nhập kho",
                Date = m.Date,
                Amount = m.TotalValue,
                Description = $"Nhập hàng từ NCC: {m.SupplierNavigation?.Name ?? m.Supplier ?? "N/A"} (Người lập: {m.CreatedBy?.FullName})"
            }));
        }

        return Json(details.OrderByDescending(d => d.Date));
    }

    // =========================================================================
    // AJAX DRILL-DOWN: Chi tiết đặt dịch vụ Spa / Loại phòng
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> GetServiceDetails(string type, int serviceId, string? fromDate, string? toDate)
    {
        DateTime start = string.IsNullOrEmpty(fromDate) 
            ? DateTime.Today.AddDays(-29) 
            : DateTime.ParseExact(fromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            
        DateTime end = string.IsNullOrEmpty(toDate) 
            ? DateTime.Today 
            : DateTime.ParseExact(toDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        var startDateTime = start.Date;
        var endDateTime = end.Date.AddDays(1).AddTicks(-1);

        var orderItems = await _context.OrderItems
            .Include(oi => oi.Order)
            .ThenInclude(o => o.Customer)
            .Where(oi => oi.Order.Date >= startDateTime && oi.Order.Date <= endDateTime)
            .Where(oi => 
                (oi.Order.OrderId.StartsWith("OD-") && (oi.Order.Status == "Chờ xử lý" || oi.Order.Status == "Đã thanh toán" || oi.Order.Status == "da thanh toan")) ||
                (oi.Order.OrderId.StartsWith("ORD-") && (oi.Order.Status == "Completed" || oi.Order.Status == "Đã hoàn thành" || oi.Order.Status == "hoan thanh" || oi.Order.Status == "completed"))
            )
            .ToListAsync();

        var details = new List<RevenueDetailItem>();

        if (type.Equals("Spa", StringComparison.OrdinalIgnoreCase))
        {
            var matched = orderItems.Where(oi => oi.SpaServiceId == serviceId);
            details.AddRange(matched.Select(oi => new RevenueDetailItem
            {
                OrderId = oi.Order.OrderId,
                CustomerName = oi.Order.Customer?.FullName ?? "Khách vãng lai",
                OrderDate = oi.Order.Date,
                Total = oi.Price * oi.Quantity,
                PaymentMethod = oi.Order.PaymentMethod,
                Status = $"SL: {oi.Quantity} lượt đặt"
            }));
        }
        else if (type.Equals("Hotel", StringComparison.OrdinalIgnoreCase) || type.Equals("Khách sạn", StringComparison.OrdinalIgnoreCase))
        {
            var matched = orderItems.Where(oi => oi.RoomTypeId == serviceId);
            details.AddRange(matched.Select(oi => new RevenueDetailItem
            {
                OrderId = oi.Order.OrderId,
                CustomerName = oi.Order.Customer?.FullName ?? "Khách vãng lai",
                OrderDate = oi.Order.Date,
                Total = oi.Price * oi.Quantity,
                PaymentMethod = oi.Order.PaymentMethod,
                Status = $"SL: {oi.Quantity} phòng x ngày"
            }));
        }

        return Json(details.OrderByDescending(d => d.OrderDate));
    }

    // =========================================================================
    // AJAX DRILL-DOWN: Chi tiết lượt đọc Blog
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> GetBlogDetails(int blogId)
    {
        var blog = await _context.Blogs
            .Include(b => b.Author)
            .FirstOrDefaultAsync(b => b.BlogId == blogId);

        if (blog == null) return NotFound("Không tìm thấy blog.");

        return Json(new TopBlogItem
        {
            BlogId = blog.BlogId,
            Title = blog.Title,
            AuthorName = blog.Author != null ? blog.Author.FullName : "Admin",
            Category = blog.Category ?? "Chung",
            ViewCount = blog.ViewCount,
            CreatedAt = blog.CreatedAt
        });
    }

    // =========================================================================
    // VIEW: Chi tiết đơn hàng cho Manager
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> OrderDetail(string id)
    {
        ViewData["ManagerNav"] = "dashboard";
        if (string.IsNullOrEmpty(id))
        {
            return NotFound("Mã đơn hàng không hợp lệ.");
        }

        var order = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.ProductSkuNavigation)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.SpaService)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.RoomType)
            .Include(o => o.HotelCheckoutStatements).ThenInclude(h => h.Items)
            .Include(o => o.HotelCheckoutStatements).ThenInclude(h => h.HotelBooking).ThenInclude(hb => hb.Pet)
            .Include(o => o.HotelCheckoutStatements).ThenInclude(h => h.HotelBooking).ThenInclude(hb => hb.Cage).ThenInclude(c => c.RoomType)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order == null)
        {
            return NotFound($"Không tìm thấy đơn hàng {id}.");
        }

        // Lấy thêm các SpaBooking thực tế liên kết bằng ghi chú có chứa [POS {id}]
        var spaBookings = await _context.SpaBookings
            .Include(sb => sb.Pet)
            .Include(sb => sb.Groomer)
            .Include(sb => sb.Service)
            .Where(sb => sb.Notes != null && sb.Notes.Contains($"[POS {id}]"))
            .ToListAsync();

        ViewBag.SpaBookings = spaBookings;

        return View(order);
    }
}
