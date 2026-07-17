/**
 * Project: Pet Store Management System (PSMS)
 * File: StockMovementController.cs
 * Author: Tran Duong
 * Date: June 11, 2026
 * Last Update: July 17, 2026
 * Description: Controller xá»­ lÃ½ cÃ¡c phiáº¿u xuáº¥t/nháº­p kho.
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

        public StockMovementController(IStockMovementService movementService, IProductService productService, IProductCategoryService categoryService, ISupplierService supplierService)
        {
            _movementService = movementService;
            _productService = productService;
            _categoryService = categoryService;
            _supplierService = supplierService;
        }

        // Hiá»ƒn thá»‹ danh sÃ¡ch cÃ¡c phiáº¿u xuáº¥t/nháº­p kho
        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, string tab = "all", string? search = null)
        {
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.Tab = tab;
            ViewBag.Search = search;

            // Láº¥y danh sÃ¡ch Ä‘Ã£ lá»c theo ngÃ y/tÃ¬m kiáº¿m Ä‘á»ƒ hiá»ƒn thá»‹
            var movements = await _movementService.GetAllMovements(fromDate, toDate, search);

            // Láº¥y TOÃ€N Bá»˜ danh sÃ¡ch (khÃ´ng lá»c ngÃ y) Ä‘á»ƒ Ä‘áº¿m badge "Chá» duyá»‡t" / "Chá» kiá»ƒm hÃ ng"
            // TrÃ¡nh bug: filter ngÃ y lÃ m máº¥t sá»‘ Ä‘Æ¡n Ä‘ang chá» xá»­ lÃ½
            var allMovementsUnfiltered = await _movementService.GetAllMovements(null, null, null);
            ViewBag.PendingManagerCount = allMovementsUnfiltered.Count(m => m.Status == "Chá» quáº£n lÃ½ duyá»‡t");
            ViewBag.PendingInspectionCount = allMovementsUnfiltered.Count(m => m.Status == "Chá» kiá»ƒm hÃ ng");

            return View(movements);
        }

        // Xuáº¥t file Excel
        [HttpGet]
        [Authorize(Roles = "warehouse,manager")]
        public async Task<IActionResult> ExportExcel(DateTime? fromDate, DateTime? toDate, string tab = "all", string? search = null)
        {
            var allMovements = await _movementService.GetAllMovements(fromDate, toDate, search);
            
            // Lá»c theo tab giá»‘ng nhÆ° View Index
            var movementsToExport = allMovements;
            if (tab == "manager")
            {
                movementsToExport = allMovements.Where(m => m.Status == "Chá» quáº£n lÃ½ duyá»‡t");
            }
            else if (tab == "inspection")
            {
                movementsToExport = allMovements.Where(m => m.Status == "Chá» kiá»ƒm hÃ ng");
            }

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("LichSuXuatNhapKho");
                var currentRow = 1;

                // Headers
                worksheet.Cell(currentRow, 1).Value = "MÃ£ Phiáº¿u";
                worksheet.Cell(currentRow, 2).Value = "NgÃ y táº¡o";
                worksheet.Cell(currentRow, 3).Value = "Loáº¡i phiáº¿u";
                worksheet.Cell(currentRow, 4).Value = "NhÃ  cung cáº¥p";
                worksheet.Cell(currentRow, 5).Value = "Tá»•ng giÃ¡ trá»‹ (VNÄ)";
                worksheet.Cell(currentRow, 6).Value = "Tráº¡ng thÃ¡i";

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

        // Hiá»ƒn thá»‹ chi tiáº¿t má»™t phiáº¿u xuáº¥t/nháº­p kho
        public async Task<IActionResult> Details(int id)
        {
            var movement = await _movementService.GetMovementById(id);
            if (movement == null) return NotFound();
            return View(movement);
        }

        // Hiá»ƒn thá»‹ form táº¡o phiáº¿u nháº­p kho (Purchase Order)
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

        // Xá»­ lÃ½ táº¡o phiáº¿u nháº­p kho
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

        // AJAX: láº¥y danh sÃ¡ch sáº£n pháº©m theo danh má»¥c
        [HttpGet]
        public async Task<IActionResult> GetProductsByCategory(int categoryId)
        {
            var allProducts = await _productService.GetProductSummary("", "active");
            var filtered = allProducts.Products
                .Where(p => p.CategoryId == categoryId)
                .Select(p => new { sku = p.Sku, name = p.Name, stock = p.Stock });
            return Json(filtered);
        }

        // AJAX: láº¥y danh sÃ¡ch nhÃ  cung cáº¥p theo danh má»¥c
        [HttpGet]
        public async Task<IActionResult> GetSuppliersByCategory(int categoryId)
        {
            var suppliers = await _supplierService.GetSuppliersByCategoryAsync(categoryId);
            var result = suppliers.Select(s => new { supplierId = s.SupplierId, name = s.Name });
            return Json(result);
        }

        // AJAX: láº¥y danh sÃ¡ch nhÃ  cung cáº¥p Ä‘Ã£ Ä‘Äƒng kÃ½ sáº£n pháº©m cá»¥ thá»ƒ
        [HttpGet]
        public async Task<IActionResult> GetSuppliersByProduct(string sku)
        {
            if (string.IsNullOrWhiteSpace(sku))
                return Json(Array.Empty<object>());

            var suppliers = await _supplierService.GetSuppliersByProductSkuAsync(sku);
            var result = suppliers.Select(s => new { supplierId = s.SupplierId, name = s.Name });
            return Json(result);
        }

        // Hiá»ƒn thá»‹ form táº¡o phiáº¿u xuáº¥t kho ná»™i bá»™
        public async Task<IActionResult> CreateExport()
        {
            ViewBag.Products = await _productService.GetProductSummary("", "active");
            return View();
        }

        // Xá»­ lÃ½ táº¡o phiáº¿u xuáº¥t kho ná»™i bá»™
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
                        CostPrice = 0 // Xuáº¥t ná»™i bá»™ khÃ´ng tÃ­nh giÃ¡ nháº­p
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

        // Hiá»ƒn thá»‹ mÃ n hÃ¬nh kiá»ƒm hÃ ng (GET) - DÃ nh cho Warehouse
        public async Task<IActionResult> Approve(int id)
        {
            var movement = await _movementService.GetMovementById(id);
            if (movement == null) return NotFound();
            if (movement.Type != "Nháº­p hÃ ng" || movement.Status != "Chá» kiá»ƒm hÃ ng")
                return RedirectToAction(nameof(Details), new { id });
            return View(movement);
        }

        // Action dÃ¹ng chung cho Manager duyá»‡t Ä‘Æ¡n hoáº·c Warehouse duyá»‡t Xuáº¥t kho
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

        // Xá»­ lÃ½ duyá»‡t phiáº¿u sau khi nhÃ¢n viÃªn kiá»ƒm hÃ ng vÃ  Ä‘iá»n HSD (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, List<string> expiryDateInputs, List<int> detailIds)
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "1");

                var expiryDates = new Dictionary<int, DateTime>();
                for (int i = 0; i < detailIds.Count; i++)
                {
                    if (i < expiryDateInputs.Count && DateTime.TryParse(expiryDateInputs[i], out var dt))
                        expiryDates[detailIds[i]] = dt;
                }

                await _movementService.ApproveMovement(id, userId, expiryDates);
            }
            catch (ServiceException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Approve), new { id });
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        // Xá»­ lÃ½ há»§y phiáº¿u xuáº¥t/nháº­p kho
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                await _movementService.CancelMovement(id);
            }
            catch (ServiceException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Details), new { id = id });
        }
    }
}
