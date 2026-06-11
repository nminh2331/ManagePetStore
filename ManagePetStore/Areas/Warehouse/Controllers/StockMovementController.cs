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

        // GET: Warehouse/StockMovement
        public async Task<IActionResult> Index()
        {
            var movements = await _movementService.GetAllMovements();
            return View(movements);
        }

        // GET: Warehouse/StockMovement/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var movement = await _movementService.GetMovementById(id);
            if (movement == null) return NotFound();
            return View(movement);
        }

        // GET: Warehouse/StockMovement/CreateImport
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

        // POST: Warehouse/StockMovement/CreateImport
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

        // GET: Warehouse/StockMovement/CreateExport
        public async Task<IActionResult> CreateExport()
        {
            ViewBag.Products = await _productService.GetProductSummary("", "active");
            return View();
        }

        // POST: Warehouse/StockMovement/CreateExport
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

        // POST: Warehouse/StockMovement/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "1");
                await _movementService.ApproveMovement(id, userId);
            }
            catch (ServiceException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Details), new { id = id });
        }

        // POST: Warehouse/StockMovement/Cancel/5
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
