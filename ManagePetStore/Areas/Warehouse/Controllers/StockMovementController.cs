/**
 * Project: Pet Store Management System (PSMS)
 * File: StockMovementController.cs
 * Author: Tran Duong
 * Date: June 11, 2026
 * Last Update: July 17, 2026
 * Description: Controller xử lý các phiếu xuất/nhập kho.
 */
using ManagePetStore.Services.Warehouse;
using ManagePetStore.Exceptions;
using ManagePetStore.Models;
using ManagePetStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ClosedXML.Excel;
using System.IO;

namespace ManagePetStore.Areas.Warehouse.Controllers
{
    [Area("Warehouse")]
    [Authorize(Roles = "warehouse,manager,admin")]
    public class StockMovementController : Controller
    {
        private readonly IStockMovementService _movementService;
        private readonly IProductService _productService;
        private readonly IProductCategoryService _categoryService;
        private readonly ISupplierService _supplierService;
        private readonly IInventoryBatchService _batchService;

        public StockMovementController(IStockMovementService movementService, IProductService productService, IProductCategoryService categoryService, ISupplierService supplierService, IInventoryBatchService batchService)
        {
            _movementService = movementService;
            _productService = productService;
            _categoryService = categoryService;
            _supplierService = supplierService;
            _batchService = batchService;
        }

        // Hiển thị danh sách các phiếu xuất/nhập kho
        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, string tab = "all", string? search = null)
        {
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.Tab = tab;
            ViewBag.Search = search;

            // Lấy danh sách đã lọc theo ngày/tìm kiếm để hiển thị
            var movements = await _movementService.GetAllMovements(fromDate, toDate, search);

            // Lấy TOÀN BỘ danh sách (không lọc ngày) để đếm badge "Chờ duyệt" / "Chờ kiểm hàng"
            // Tránh bug: filter ngày làm mất số đơn đang chờ xử lý
            var allMovementsUnfiltered = await _movementService.GetAllMovements(null, null, null);
            ViewBag.PendingManagerCount = allMovementsUnfiltered.Count(m => m.Status == "Chờ quản lý duyệt");
            ViewBag.PendingInspectionCount = allMovementsUnfiltered.Count(m => m.Status == "Chờ kiểm hàng");

            return View(movements);
        }

        // Xuất file Excel
        [HttpGet]
        [Authorize(Roles = "warehouse,manager")]
        public async Task<IActionResult> ExportExcel(DateTime? fromDate, DateTime? toDate, string tab = "all", string? search = null)
        {
            var allMovements = await _movementService.GetAllMovements(fromDate, toDate, search);
            
            // Lọc theo tab giống như View Index
            var movementsToExport = allMovements;
            if (tab == "manager")
            {
                movementsToExport = allMovements.Where(m => m.Status == "Chờ quản lý duyệt");
            }
            else if (tab == "inspection")
            {
                movementsToExport = allMovements.Where(m => m.Status == "Chờ kiểm hàng");
            }

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("LichSuXuatNhapKho");
                var currentRow = 1;

                // Headers
                worksheet.Cell(currentRow, 1).Value = "Mã Phiếu";
                worksheet.Cell(currentRow, 2).Value = "Ngày tạo";
                worksheet.Cell(currentRow, 3).Value = "Loại phiếu";
                worksheet.Cell(currentRow, 4).Value = "Nhà cung cấp";
                worksheet.Cell(currentRow, 5).Value = "Tổng giá trị (VNĐ)";
                worksheet.Cell(currentRow, 6).Value = "Trạng thái";

                // Format Headers
                var headerRange = worksheet.Range(1, 1, 1, 6);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Data Rows
                foreach (var m in movementsToExport)
                {
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "MH-" + m.MovementId.ToString("D5");
                    worksheet.Cell(currentRow, 2).Value = m.Date.ToString("dd/MM/yyyy HH:mm");
                    worksheet.Cell(currentRow, 3).Value = m.Type;
                    worksheet.Cell(currentRow, 4).Value = m.Supplier ?? "N/A";
                    worksheet.Cell(currentRow, 5).Value = m.TotalValue;
                    worksheet.Cell(currentRow, 6).Value = m.Status;
                }

                // Format Columns
                worksheet.Column(5).Style.NumberFormat.Format = "#,##0";
                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    var fileName = $"LichSuXuatNhapKho_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
        }

        // Hiển thị chi tiết một phiếu xuất/nhập kho
        public async Task<IActionResult> Details(int id)
        {
            var movement = await _movementService.GetMovementById(id);
            if (movement == null) return NotFound();
            return View(movement);
        }

        // Hiển thị form tạo phiếu nhập kho (Purchase Order)
        public async Task<IActionResult> CreateImport(string? productSku)
        {
            if (!string.IsNullOrEmpty(productSku))
            {
                var product = await _productService.GetProductBySku(productSku);
                ViewBag.PrefillProduct = product;
            }
            ViewBag.Categories = (await _categoryService.GetCategorySummary()).Categories;
            ViewBag.Products = await _productService.GetProductSummary("", "active");
            return View();
        }

        // Xử lý tạo phiếu nhập kho
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateImport(int? SupplierId, string ProductSku, int Quantity, decimal CostPrice)
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "1");
                var details = new List<StockMovementDetail>
                {
                    new StockMovementDetail
                    {
                        ProductSku = ProductSku,
                        Quantity = Quantity,
                        CostPrice = CostPrice
                    }
                };
                
                await _movementService.CreateImportOrder(userId, SupplierId, details);
                return RedirectToAction(nameof(Index));
            }
            catch (ServiceException ex)
            {
                ModelState.AddModelError("", ex.Message);
                ViewBag.Categories = (await _categoryService.GetCategorySummary()).Categories;
                ViewBag.Products = await _productService.GetProductSummary("", "active");
                return View();
            }
        }

        // AJAX: lấy danh sách sản phẩm theo danh mục
        [HttpGet]
        public async Task<IActionResult> GetProductsByCategory(int categoryId)
        {
            var allProducts = await _productService.GetProductSummary("", "active");
            var filtered = allProducts.Products
                .Where(p => p.CategoryId == categoryId)
                .Select(p => new { sku = p.Sku, name = p.Name, stock = p.Stock });
            return Json(filtered);
        }

        // AJAX: lấy danh sách nhà cung cấp theo danh mục
        [HttpGet]
        public async Task<IActionResult> GetSuppliersByCategory(int categoryId)
        {
            var suppliers = await _supplierService.GetSuppliersByCategoryAsync(categoryId);
            var result = suppliers.Select(s => new { supplierId = s.SupplierId, name = s.Name });
            return Json(result);
        }

        // AJAX: lấy danh sách nhà cung cấp đã đăng ký sản phẩm cụ thể
        [HttpGet]
        public async Task<IActionResult> GetSuppliersByProduct(string sku)
        {
            if (string.IsNullOrWhiteSpace(sku))
                return Json(Array.Empty<object>());

            var suppliers = await _supplierService.GetSuppliersByProductSkuAsync(sku);
            var result = suppliers.Select(s => new { supplierId = s.SupplierId, name = s.Name });
            return Json(result);
        }

        // Hiển thị form tạo phiếu xuất kho nội bộ
        public async Task<IActionResult> CreateExport()
        {
            ViewBag.Products = await _productService.GetProductSummary("", "active");
            return View();
        }

        // Xử lý tạo phiếu xuất kho nội bộ
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateExport(string Note, string ProductSku, int Quantity)
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "1");
                var details = new List<StockMovementDetail>
                {
                    new StockMovementDetail
                    {
                        ProductSku = ProductSku,
                        Quantity = Quantity,
                        CostPrice = 0 // Xuất nội bộ không tính giá nhập
                    }
                };
                
                await _movementService.CreateInternalExport(userId, Note, details);
                return RedirectToAction(nameof(Index));
            }
            catch (ServiceException ex)
            {
                ModelState.AddModelError("", ex.Message);
                ViewBag.Products = await _productService.GetProductSummary("", "active");
                return View();
            }
        }

        // Hiển thị màn hình kiểm hàng (GET) - Dành cho Warehouse
        public async Task<IActionResult> Approve(int id)
        {
            var movement = await _movementService.GetMovementById(id);
            if (movement == null) return NotFound();
            if (movement.Status != "Chờ kiểm hàng" || (movement.Type != "Nhập hàng" && movement.Type != "Nhập kho (Hủy đơn)"))
                return RedirectToAction(nameof(Details), new { id });
            
            if (movement.Type == "Nhập kho (Hủy đơn)")
            {
                var productBatches = new Dictionary<string, List<InventoryBatch>>();
                foreach (var detail in movement.StockMovementDetails)
                {
                    if (!productBatches.ContainsKey(detail.ProductSku))
                    {
                        var batches = await _batchService.GetBatchesByProductSku(detail.ProductSku);
                        productBatches[detail.ProductSku] = batches.ToList();
                    }
                }
                ViewBag.ProductBatches = productBatches;
            }

            return View(movement);
        }

        // Action dùng chung cho Manager duyệt đơn hoặc Warehouse duyệt Xuất kho
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveStatus(int id)
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "1");
                await _movementService.ApproveMovement(id, userId, null);
            }
            catch (ServiceException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        // Xử lý duyệt phiếu sau khi nhân viên kiểm hàng và điền HSD (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, List<string> expiryDateInputs, List<int> detailIds, string? allocationsJson)
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "1");

                List<ManagePetStore.Services.Warehouse.BatchAllocation>? allocations = null;
                if (!string.IsNullOrWhiteSpace(allocationsJson))
                {
                    allocations = System.Text.Json.JsonSerializer.Deserialize<List<ManagePetStore.Services.Warehouse.BatchAllocation>>(
                        allocationsJson, 
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                }

                var expiryDates = new Dictionary<int, DateTime>();
                for (int i = 0; i < detailIds.Count; i++)
                {
                    if (i < expiryDateInputs.Count && DateTime.TryParse(expiryDateInputs[i], out var dt))
                        expiryDates[detailIds[i]] = dt;
                }

                await _movementService.ApproveMovement(id, userId, expiryDates, allocations);
            }
            catch (ServiceException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Approve), new { id });
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        // Xử lý hủy phiếu xuất/nhập kho
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string? reason)
        {
            try
            {
                await _movementService.CancelMovement(id, reason);
            }
            catch (ServiceException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Details), new { id = id });
        }
    }
}
