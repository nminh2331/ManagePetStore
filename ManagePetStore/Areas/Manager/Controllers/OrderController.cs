using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagePetStore.Models;
using System.Linq;

namespace ManagePetStore.Areas.Manager.Controllers
{
    [Area("Manager")]
    [Authorize(Roles = "manager")]
    public class OrderController : Controller
    {
        private readonly PetStoreManagementContext _context;

        public OrderController(PetStoreManagementContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var orders = await _context.Orders
                .Include(o => o.Customer)
                .OrderByDescending(o => o.Date)
                .ToListAsync();

            return View(orders);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(string orderId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order != null)
            {
                order.Status = "approved";
                _context.Entry(order).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã duyệt đơn hàng {FormatDisplayOrderId(orderId)} thành công.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ship(string orderId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order != null)
            {
                order.Status = "delivering";
                _context.Entry(order).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đơn hàng {FormatDisplayOrderId(orderId)} đang được giao hàng.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(string orderId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order != null)
            {
                order.Status = "completed";
                _context.Entry(order).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đơn hàng {FormatDisplayOrderId(orderId)} đã hoàn thành thành công.";
            }
            return RedirectToAction(nameof(Index));
        }

        private static string FormatDisplayOrderId(string orderId)
        {
            var parts = orderId.Split('-', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return $"#OD-{parts[^1]}";
            }
            return $"#{orderId}";
        }
    }
}
