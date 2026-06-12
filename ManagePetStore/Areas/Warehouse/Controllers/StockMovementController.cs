/**
 * Project: Pet Store Management System (PSMS)
 * File: StockMovementController.cs
 * Author: Tran Duong
 * Date: June 11, 2026
 * Description: Controller xử lý các phiếu xuất/nhập kho.
 */
using ManagePetStore.Areas.Warehouse.Services;
using ManagePetStore.Exceptions;
using ManagePetStore.Models;
using ManagePetStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ManagePetStore.Areas.Warehouse.Controllers
{
    [Area("Warehouse")]
    [Authorize(Roles = "warehouse,admin")]
    public class StockMovementController : Controller
    {
        private readonly IStockMovementService _movementService;
        private readonly IProductService _productService;

        public StockMovementController(IStockMovementService movementService, IProductService productService)
        {
            _movementService = movementService;
            _productService = productService;
        }

        // Hiển thị danh sách các phiếu xuất/nhập kho
        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, string tab = "all")
        {
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.Tab = tab;
            var movements = await _movementService.GetAllMovements(fromDate, toDate);
            return View(movements);
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
            ViewBag.Products = await _productService.GetProductSummary("", "active");
            return View();
        }

        // Xử lý tạo phiếu nhập kho
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateImport(string Supplier, string ProductSku, int Quantity, decimal CostPrice)
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
                
                await _movementService.CreateImportOrder(userId, Supplier, details);
                return RedirectToAction(nameof(Index));
            }
            catch (ServiceException ex)
            {
                ModelState.AddModelError("", ex.Message);
                ViewBag.Products = await _productService.GetProductSummary("", "active");
                return View();
            }
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

        // Hiển thị màn hình kiểm hàng (GET)
        public async Task<IActionResult> Approve(int id)
        {
            var movement = await _movementService.GetMovementById(id);
            if (movement == null) return NotFound();
            if (movement.Type != "Nhập hàng" || movement.Status != "Chờ duyệt")
                return RedirectToAction(nameof(Details), new { id });
            return View(movement);
        }

        // Xử lý duyệt phiếu sau khi nhân viên kiểm hàng và điền HSD (POST)
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

        // Xử lý hủy phiếu xuất/nhập kho
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
