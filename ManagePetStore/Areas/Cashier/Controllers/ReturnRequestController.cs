using System.Security.Claims;
using ManagePetStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Areas.Cashier.Controllers
{
    [Area("Cashier")]
    [Authorize(Roles = "cashier")]
    public class ReturnRequestController : Controller
    {
        private readonly PetStoreManagementContext _context;

        public ReturnRequestController(PetStoreManagementContext context)
        {
            _context = context;
        }

        private async Task<int?> GetCurrentUserIdAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return null;
            return int.TryParse(userIdClaim.Value, out var id) ? id : null;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var allRequests = await _context.ReturnRequests
                .Include(r => r.Customer)
                .Include(r => r.Order)
                .Include(r => r.ReturnRequestItems)
                    .ThenInclude(ri => ri.SkuNavigation)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.SubmittedRequests = allRequests.Where(r => r.Status == "Submitted").ToList();
            ViewBag.WaitingRequests = allRequests.Where(r => r.Status == "WaitingForReturn").ToList();
            ViewBag.ProcessedRequests = allRequests.Where(r => r.Status != "Submitted" && r.Status != "WaitingForReturn").ToList();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveOnline(int id)
        {
            var request = await _context.ReturnRequests.FirstOrDefaultAsync(r => r.RequestId == id);
            if (request == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy yêu cầu trả hàng.";
                return RedirectToAction(nameof(Index));
            }

            if (request.Status != "Submitted")
            {
                TempData["ErrorMessage"] = "Yêu cầu không ở trạng thái chờ duyệt online.";
                return RedirectToAction(nameof(Index));
            }

            var userId = await GetCurrentUserIdAsync();

            request.Status = "WaitingForReturn";
            request.ProcessedBy = userId;
            request.ProcessedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã phê duyệt online yêu cầu #REQ-{id}. Chờ khách mang hàng đến quầy.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectOnline(int id, string rejectReason)
        {
            if (string.IsNullOrWhiteSpace(rejectReason))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập lý do từ chối.";
                return RedirectToAction(nameof(Index));
            }

            var request = await _context.ReturnRequests.FirstOrDefaultAsync(r => r.RequestId == id);
            if (request == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy yêu cầu trả hàng.";
                return RedirectToAction(nameof(Index));
            }

            if (request.Status != "Submitted")
            {
                TempData["ErrorMessage"] = "Yêu cầu không ở trạng thái chờ duyệt online.";
                return RedirectToAction(nameof(Index));
            }

            var userId = await GetCurrentUserIdAsync();

            var originalNotes = request.Notes ?? "";
            var imgPart = "";
            if (originalNotes.Contains("IMAGES:"))
            {
                var parts = originalNotes.Split('|');
                var img = parts.FirstOrDefault(p => p.StartsWith("IMAGES:"));
                if (img != null) imgPart = img;
            }

            request.Status = "OnlineRejected";
            request.Notes = (string.IsNullOrEmpty(imgPart) ? "" : imgPart + "|") + "REJECT:" + rejectReason.Trim();
            request.ProcessedBy = userId;
            request.ProcessedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã từ chối duyệt online yêu cầu #REQ-{id}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPhysicalReturn(int id)
        {
            var request = await _context.ReturnRequests
                .Include(r => r.ReturnRequestItems)
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (request == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy yêu cầu trả hàng.";
                return RedirectToAction(nameof(Index));
            }

            if (request.Status != "WaitingForReturn")
            {
                TempData["ErrorMessage"] = "Yêu cầu không ở trạng thái chờ mang hàng đến quầy.";
                return RedirectToAction(nameof(Index));
            }

            var userId = await GetCurrentUserIdAsync();

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // 1. Update request status
                    request.Status = "Success";
                    request.ProcessedBy = userId;
                    request.ProcessedAt = DateTime.Now;
                    _context.Entry(request).State = EntityState.Modified;

                    // 2. Add refund amount to Customer's Wallet
                    var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.CustomerId == request.CustomerId);
                    if (wallet == null)
                    {
                        wallet = new Wallet
                        {
                            CustomerId = request.CustomerId,
                            Balance = 0,
                            Status = "Active",
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };
                        _context.Wallets.Add(wallet);
                        await _context.SaveChangesAsync(); // save to get WalletId
                    }

                    wallet.Balance += request.RefundAmount;
                    wallet.UpdatedAt = DateTime.Now;
                    _context.Entry(wallet).State = EntityState.Modified;

                    // 3. Create wallet transaction
                    var walletTransaction = new WalletTransaction
                    {
                        WalletId = wallet.WalletId,
                        Amount = request.RefundAmount,
                        Type = "Refund",
                        Description = $"Hoàn tiền trả hàng yêu cầu #REQ-{request.RequestId} (Đơn #OD-{request.OrderId.Split('-')[^1]})",
                        OrderId = request.OrderId,
                        TransactionDate = DateTime.Now
                    };
                    _context.WalletTransactions.Add(walletTransaction);

                    // 4. Restock items returned
                    foreach (var item in request.ReturnRequestItems)
                    {
                        var product = await _context.Products.FirstOrDefaultAsync(p => p.Sku == item.Sku);
                        if (product != null)
                        {
                            product.Stock += item.Quantity;
                            _context.Entry(product).State = EntityState.Modified;
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = $"Đã xác nhận hoàn thành trả hàng yêu cầu #REQ-{id}. Tiền đã được hoàn vào ví của khách.";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = "Có lỗi xảy ra khi xác nhận trả hàng: " + ex.Message;
                }
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPhysicalReturn(int id, string rejectReason)
        {
            if (string.IsNullOrWhiteSpace(rejectReason))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập lý do từ chối nhận hàng.";
                return RedirectToAction(nameof(Index));
            }

            var request = await _context.ReturnRequests.FirstOrDefaultAsync(r => r.RequestId == id);
            if (request == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy yêu cầu trả hàng.";
                return RedirectToAction(nameof(Index));
            }

            if (request.Status != "WaitingForReturn")
            {
                TempData["ErrorMessage"] = "Yêu cầu không ở trạng thái chờ mang hàng đến quầy.";
                return RedirectToAction(nameof(Index));
            }

            var userId = await GetCurrentUserIdAsync();

            var originalNotes = request.Notes ?? "";
            var imgPart = "";
            if (originalNotes.Contains("IMAGES:"))
            {
                var parts = originalNotes.Split('|');
                var img = parts.FirstOrDefault(p => p.StartsWith("IMAGES:"));
                if (img != null) imgPart = img;
            }

            request.Status = "PhysicalRejected";
            request.Notes = (string.IsNullOrEmpty(imgPart) ? "" : imgPart + "|") + "REJECT:" + rejectReason.Trim();
            request.ProcessedBy = userId;
            request.ProcessedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã từ chối nhận hàng thực tế tại quầy cho yêu cầu #REQ-{id}.";
            return RedirectToAction(nameof(Index));
        }
    }
}
