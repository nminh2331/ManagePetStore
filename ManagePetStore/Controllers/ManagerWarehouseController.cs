using ManagePetStore.Services.Customer;
using ManagePetStore.Models.ManagerModels;
using ManagePetStore.Models.CustomerModels;
/**
 * Project: Pet Store Management System (PSMS)
 * File: WarehouseController.cs
 * Author: Tran Duong
 * Date: June 17, 2026
 * Description: Controller xử lý duyệt phiếu nhập kho cho Manager.
 */
using System.Security.Claims;
using ManagePetStore.Services.Warehouse;
using ManagePetStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagePetStore.Controllers;

[Authorize(Roles = "manager,admin")]
[Route("Manager/Warehouse/{action=Index}/{id?}")]
public class ManagerWarehouseController : Controller
{
    private readonly IStockMovementService _movementService;

    public ManagerWarehouseController(IStockMovementService movementService)
    {
        _movementService = movementService;
    }

    // Danh sach phieu kho - Manager xem de duyet
    public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, string tab = "manager", string? search = null)
    {
        ViewData["ManagerNav"] = "warehouse";
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
        ViewBag.Tab = tab;
        ViewBag.Search = search;

        var movements = await _movementService.GetAllMovements(fromDate, toDate, search);
        return View(movements);
    }

    // Chi tiet phieu kho
    public async Task<IActionResult> Details(int id)
    {
        ViewData["ManagerNav"] = "warehouse";
        var movement = await _movementService.GetMovementById(id);
        if (movement == null) return NotFound();
        return View(movement);
    }

    // Manager duyet don (Cho quan ly duyet -> Cho kiem hang)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        try
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "1");
            await _movementService.ApproveMovement(id, userId, null);
            TempData["SuccessMessage"] = "Don nhap hang da duoc duyet. Thu kho se tien hanh kiem hang.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // Manager tu choi don (Cho quan ly duyet -> Da huy)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        try
        {
            await _movementService.CancelMovement(id);
            TempData["SuccessMessage"] = "Da tu choi phieu.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }
}




