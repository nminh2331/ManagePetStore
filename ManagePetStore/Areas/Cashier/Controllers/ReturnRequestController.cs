// HÀ HOÀNG HIỆP CODE - LUỒNG TRẢ HÀNG & HOÀN TIỀN: USE CASE PROCESS REFUND (QUẢN LÝ VÀ XỬ LÝ TRẢ HÀNG PHÍA THU NGÂN)
using System.Security.Claims;
using ManagePetStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagePetStore.Hubs;
using Microsoft.AspNetCore.SignalR;
using ManagePetStore.Services.Warehouse;

namespace ManagePetStore.Areas.Cashier.Controllers
{
    [Area("Cashier")]
    [Authorize(Roles = "cashier")]
    public class ReturnRequestController : Controller
    {
        private readonly PetStoreManagementContext _context;
        private readonly IHubContext<HotelCareHub> _hubContext;
        private readonly IStockMovementService _stockMovementService;
        private readonly IInventoryBatchService _inventoryBatchService;

        public ReturnRequestController(PetStoreManagementContext context, IHubContext<HotelCareHub> hubContext, IStockMovementService stockMovementService, IInventoryBatchService inventoryBatchService)
        {
            _context = context;
            _hubContext = hubContext;
            _stockMovementService = stockMovementService;
            _inventoryBatchService = inventoryBatchService;
        }

        private async Task<int?> GetCurrentUserIdAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return null;
            return int.TryParse(userIdClaim.Value, out var id) ? id : null;
        }

        /// <summary>
        /// LUỒNG PROCESS REFUND: Trang danh sách quản lý Yêu cầu trả hàng
        /// - Hiển thị 3 danh sách phân trang:
        ///   + Submitted: Yêu cầu mới gửi, chờ duyệt Online.
        ///   + WaitingForReturn: Đã duyệt Online, chờ khách mang hàng đến quầy trong 7 ngày.
        ///   + Processed: Các yêu cầu đã hoàn tất trả tiền hoặc đã từ chối.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(string activeTab = "submitted", string searchStr = "", int pageS = 1, int pageW = 1, int pageP = 1)
        {
            ViewBag.ActiveTab = activeTab;
            ViewBag.SearchStr = searchStr;

            var allRequestsQuery = _context.ReturnRequests
                .Include(r => r.Customer)
                .Include(r => r.Order)
                .Include(r => r.ReturnRequestItems)
                    .ThenInclude(ri => ri.SkuNavigation)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchStr))
            {
                allRequestsQuery = allRequestsQuery.Where(r => r.OrderId.Contains(searchStr));
            }

            var allRequests = await allRequestsQuery.OrderByDescending(r => r.CreatedAt).ToListAsync();

            var submittedList = allRequests.Where(r => r.Status == "Submitted").ToList();
            var waitingList = allRequests.Where(r => r.Status == "WaitingForReturn").ToList();
            var processedList = allRequests.Where(r => r.Status != "Submitted" && r.Status != "WaitingForReturn").ToList();

            int pageSize = 5;

            ViewBag.SubmittedTotalPages = (int)Math.Ceiling(submittedList.Count / (double)pageSize);
            ViewBag.CurrentPageS = pageS;
            ViewBag.SubmittedRequests = submittedList.Skip((pageS - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.WaitingTotalPages = (int)Math.Ceiling(waitingList.Count / (double)pageSize);
            ViewBag.CurrentPageW = pageW;
            ViewBag.WaitingRequests = waitingList.Skip((pageW - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.ProcessedTotalPages = (int)Math.Ceiling(processedList.Count / (double)pageSize);
            ViewBag.CurrentPageP = pageP;
            ViewBag.ProcessedRequests = processedList.Skip((pageP - 1) * pageSize).Take(pageSize).ToList();

            return View();
        }

        /// <summary>
        /// LUỒNG PROCESS REFUND (BƯỚC 1: DUYỆT ONLINE): Phê duyệt yêu cầu trả hàng qua ảnh/video minh chứng
        /// - Thu ngân kiểm tra hình ảnh/video sản phẩm lỗi do khách gửi.
        /// - Nếu đủ điều kiện -> Bấm "Duyệt online" (ApproveOnline).
        /// - KẾT QUẢ: Chuyển trạng thái sang "WaitingForReturn" (Chờ khách mang sản phẩm đến quầy trong vòng 7 ngày), đồng thời phát thông báo realtime (SignalR/CustomerNotification) cho Khách hàng.
        /// </summary>
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

            // Tạo thông báo gửi cho Khách hàng
            var notif = new CustomerNotification
            {
                CustomerId = request.CustomerId,
                Type = "ReturnRequest",
                Title = "Yêu cầu trả hàng đã được duyệt",
                Message = $"Yêu cầu trả hàng #REQ-{id} đã được duyệt online. Vui lòng mang sản phẩm đến cửa hàng trong vòng 7 ngày để hoàn tất thủ tục trả hàng.",
                CreatedAt = DateTime.Now,
                IsRead = false,
                LinkUrl = "/Customer/Account/Profile?activeTab=return"
            };
            _context.CustomerNotifications.Add(notif);

            await _context.SaveChangesAsync();

            // Phát thông báo Realtime qua SignalR
            await _hubContext.Clients.All.SendAsync("CareLogUpdated", new {
                notificationId = notif.NotificationId,
                title = notif.Title,
                message = notif.Message,
                occurredAt = notif.CreatedAt.ToString("o")
            });

            TempData["SuccessMessage"] = $"Đã phê duyệt online yêu cầu #REQ-{id}. Chờ khách mang hàng đến quầy.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// LUỒNG PROCESS REFUND (BƯỚC 1: TỪ CHỐI ONLINE): Từ chối duyệt online
        /// - VALIDATION: Bắt buộc Thu ngân nhập lý do từ chối rõ ràng.
        /// - KẾT QUẢ: Chuyển trạng thái đơn trả thành "OnlineRejected" và ghi nhận lý do từ chối.
        /// </summary>
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

        /// <summary>
        /// LUỒNG PROCESS REFUND (BƯỚC 2: HOÀN TIỀN TẠI QUẦY): Xác nhận nhận hàng và hoàn tiền vào Ví điện tử
        /// - Khách hàng mang sản phẩm đến quầy, Thu ngân mở tab "Chờ trả tại quầy" (WaitingForReturn).
        /// - Thu ngân nhận sản phẩm thực tế từ khách và xác minh/đối chiếu trực tiếp với hình ảnh/video lỗi trước đó.
        /// - Nếu đồng ý hoàn tiền -> Bấm "Xác nhận nhận hàng & hoàn tiền" (ConfirmPhysicalReturn).
        /// - KẾT QUẢ NGHIỆP VỤ:
        ///   1. Chuyển trạng thái Yêu cầu trả hàng thành "Success".
        ///   2. HOÀN TIỀN VÀO VÍ ĐIỆN TỬ: Số tiền refund (request.RefundAmount) được TỰ ĐỘNG CHUYỂN THẲNG vào số dư Ví điện tử (Wallet.Balance) của khách hàng.
        ///   3. Ghi nhận giao dịch lịch sử ví (WalletTransaction: Type = "Refund").
        ///   4. HOÀN TỒN KHO: Sản phẩm trả được làm thủ tục nhập lại kho hàng.
        /// </summary>
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
                    // 1. Cập nhật trạng thái yêu cầu trả hàng thành Thành công (Success)
                    request.Status = "Success";
                    request.ProcessedBy = userId;
                    request.ProcessedAt = DateTime.Now;
                    _context.Entry(request).State = EntityState.Modified;

                    // 2. CHUYỂN TIỀN HOÀN VÀO VÍ ĐIỆN TỬ (E-WALLET) CỦA KHÁCH HÀNG
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
                        await _context.SaveChangesAsync();
                    }

                    // Cộng số tiền hoàn vào Ví điện tử của khách hàng
                    wallet.Balance += request.RefundAmount;
                    wallet.UpdatedAt = DateTime.Now;
                    _context.Entry(wallet).State = EntityState.Modified;

                    // 3. Tạo biến động giao dịch Ví (WalletTransaction)
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

                    // 4. Nhập kho lại sản phẩm đã nhận từ khách hàng
                    var systemStockDetails = new List<StockMovementDetail>();
                    foreach (var item in request.ReturnRequestItems)
                    {
                        var product = await _context.Products.FirstOrDefaultAsync(p => p.Sku == item.Sku);
                        if (product != null)
                        {
                            systemStockDetails.Add(new StockMovementDetail
                            {
                                ProductSku = item.Sku,
                                Quantity = item.Quantity,
                                CostPrice = item.RefundPrice
                            });
                        }
                    }

                    if (systemStockDetails.Any())
                    {
                        await _stockMovementService.CreateSystemMovement(
                            systemUserId: userId ?? 1,
                            type: "Nhập kho (Khách trả hàng)",
                            status: "Chờ kiểm hàng",
                            supplier: $"Khách trả hàng - REQ-{request.RequestId}",
                            totalValue: systemStockDetails.Sum(d => d.Quantity * d.CostPrice),
                            details: systemStockDetails
                        );
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

        /// <summary>
        /// LUỒNG PROCESS REFUND (BƯỚC 2: TỪ CHỐI TẠI QUẦY): Từ chối nhận sản phẩm thực tế khi khách mang đến
        /// - Sau khi đối chiếu sản phẩm thực tế với ảnh/video minh chứng, nếu sản phẩm không đáp ứng điều kiện trả.
        /// - VALIDATION: Bắt buộc Thu ngân nhập Lý do từ chối nhận hàng tại quầy.
        /// - KẾT QUẢ: Chuyển trạng thái sang "PhysicalRejected", nhập lý do từ chối và thông báo trực tiếp cho khách hàng.
        /// </summary>
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
