using ManagePetStore.Exceptions;
using ManagePetStore.Model;
using ManagePetStore.Models;
using ManagePetStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ManagePetStore.Controllers;

[Authorize(Roles = "Admin,StoreManager")]
public class RoomController : Controller
{
    private readonly IRoomService _service;

    public RoomController(IRoomService service)
    {
        _service = service;
    }

    // ─── GET /Room ─────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Index(string? statusFilter)
    {
        var viewModel = _service.GetAll(statusFilter);
        return View(viewModel);
    }

    // ─── GET /Room/Create ──────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Create()
    {
        LoadViewBags();
        return View(new RoomViewModel { Status = RoomStatus.Available });
    }

    // ─── POST /Room/Create ─────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(RoomViewModel model)
    {
        if (!ModelState.IsValid)
        {
            LoadViewBags(model.RoomType, model.Status);
            return View(model);
        }

        try
        {
            _service.Create(model);
            TempData["Success"] = $"Đã thêm chuồng «{model.RoomCode}» thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (ServiceException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            LoadViewBags(model.RoomType, model.Status);
            return View(model);
        }
    }

    // ─── GET /Room/Edit/{id} ───────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var model = _service.GetById(id);
        if (model is null) return NotFound();

        LoadViewBags(model.RoomType, model.Status);
        return View(model);
    }

    // ─── POST /Room/Edit ───────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(RoomViewModel model)
    {
        if (!ModelState.IsValid)
        {
            LoadViewBags(model.RoomType, model.Status);
            return View(model);
        }

        try
        {
            _service.Update(model);
            TempData["Success"] = $"Đã cập nhật chuồng «{model.RoomCode}» thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (ServiceException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            LoadViewBags(model.RoomType, model.Status);
            return View(model);
        }
    }

    // ─── POST /Room/Delete ─────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        try
        {
            _service.Delete(id);
            TempData["Success"] = "Đã xóa chuồng thành công!";
        }
        catch (ServiceException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    // ─── POST /Room/UpdateStatus ───────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateStatus(int id, string status)
    {
        try
        {
            _service.UpdateStatus(id, status);
            TempData["Success"] = "Đã cập nhật trạng thái!";
        }
        catch (ServiceException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    // ─── Private helpers ───────────────────────────────────────────────────────

    private void LoadViewBags(string? selectedType = null, string? selectedStatus = null)
    {
        ViewBag.RoomTypes = new SelectList(RoomService.RoomTypes
            .Select(t => new { Value = t, Text = t }),
            "Value", "Text", selectedType);

        ViewBag.Statuses = new SelectList(new[]
        {
            new { Value = RoomStatus.Available,   Text = "Trống (Available)" },
            new { Value = RoomStatus.Occupied,    Text = "Đang dùng (Occupied)" },
            new { Value = RoomStatus.Cleaning,    Text = "Đang dọn (Cleaning)" },
            new { Value = RoomStatus.Maintenance, Text = "Bảo trì (Maintenance)" }
        }, "Value", "Text", selectedStatus ?? RoomStatus.Available);
    }
}
