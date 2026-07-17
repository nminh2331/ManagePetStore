using System.Data;
using System.Security.Claims;
using ManagePetStore.Areas.Customer.Models;
using ManagePetStore.Models;
using ManagePetStore.Services;
using ManagePetStore.Services.Warehouse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Areas.Customer.Controllers;

[Area("Customer")]
[Authorize]
public class HotelBookingController : Controller
{
    private static readonly string[] BlockingStatuses = ["Đã đặt", "Active", "Đang ở"];

    private readonly PetStoreManagementContext _context;
    private readonly IHotelBookingHistoryService _historyService;
    private readonly IInventoryBatchService _inventoryBatchService;
    private readonly IHotelEmailService _hotelEmailService;
    private readonly ILogger<HotelBookingController> _logger;

    public HotelBookingController(
        PetStoreManagementContext context,
        IHotelBookingHistoryService historyService,
        IInventoryBatchService inventoryBatchService,
        IHotelEmailService hotelEmailService,
        ILogger<HotelBookingController> logger)
    {
        _context = context;
        _historyService = historyService;
        _inventoryBatchService = inventoryBatchService;
        _hotelEmailService = hotelEmailService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm, string statusFilter = "all", int page = 1)
    {
        var layout = await BuildSidebarViewModelAsync("appointments");
        if (layout == null)
        {
            return RedirectToAction("Login", "Account", new { area = "Customer" });
        }

        // Tải lịch đặt khách sạn
        var bookings = await _context.HotelBookings
            .AsNoTracking()
            .Include(b => b.Pet)
            .Include(b => b.Cage)
                .ThenInclude(c => c.RoomType)
            .Where(b => b.CustomerId == layout.Customer.CustomerId)
            .OrderByDescending(b => b.HotelBookingId)
            .ToListAsync();

        var mappedBookings = bookings.Select(MapToListItem).ToList();
        var normalizedSearch = searchTerm?.Trim() ?? "";
        var normalizedStatus = string.IsNullOrWhiteSpace(statusFilter)
            ? "all"
            : statusFilter.Trim().ToLowerInvariant();

        IEnumerable<HotelBookingListItemViewModel> filteredBookings = mappedBookings;

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            filteredBookings = filteredBookings.Where(b =>
                b.DisplayBookingId.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                b.PetName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                b.CageId.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                b.RoomTypeName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                b.Status.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
        }

        filteredBookings = normalizedStatus switch
        {
            "reserved" => filteredBookings.Where(b => b.StatusKey == "reserved"),
            "active" => filteredBookings.Where(b => b.StatusKey == "active"),
            "completed" => filteredBookings.Where(b => b.StatusKey == "completed"),
            "cancelled" => filteredBookings.Where(b => b.StatusKey == "cancelled"),
            _ => filteredBookings
        };

        var filteredBookingList = filteredBookings.ToList();
        var currentPage = page < 1 ? 1 : page;
        var pageSize = new HotelBookingHistoryPageViewModel().PageSize;
        var totalFilteredItems = filteredBookingList.Count;
        var totalPages = totalFilteredItems == 0 ? 0 : (int)Math.Ceiling(totalFilteredItems / (double)pageSize);

        if (totalPages > 0 && currentPage > totalPages)
        {
            currentPage = totalPages;
        }

        var model = new HotelBookingHistoryPageViewModel
        {
            User = layout.User,
            Customer = layout.Customer,
            ActiveNav = layout.ActiveNav,
            Bookings = mappedBookings,
            SearchTerm = normalizedSearch,
            StatusFilter = normalizedStatus,
            Page = totalPages == 0 ? 1 : currentPage,
            TotalFilteredItems = totalFilteredItems,
            TotalPages = totalPages
        };

        model.VisibleBookings = filteredBookingList
            .Skip((model.Page - 1) * model.PageSize)
            .Take(model.PageSize)
            .ToList();

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var layout = await BuildSidebarViewModelAsync("appointments");
        if (layout == null)
        {
            return RedirectToAction("Login", "Account", new { area = "Customer" });
        }

        var booking = await _historyService.GetDetailAsync(id, layout.Customer.CustomerId);
        if (booking == null)
        {
            return NotFound();
        }

        var pendingRequest = await _context.HotelCageChangeRequests
            .AsNoTracking()
            .Where(request => request.HotelBookingId == id && request.Status == "Pending")
            .OrderByDescending(request => request.RequestedAt)
            .Select(request => new HotelCageChangeRequestItemViewModel
            {
                ChangeRequestId = request.ChangeRequestId,
                SourceCageId = request.SourceCageId,
                TargetCageId = request.TargetCageId,
                Reason = request.Reason,
                Status = request.Status,
                EstimatedPriceDifference = request.PriceDifferenceSnapshot,
                RequestedAt = request.RequestedAt
            })
            .FirstOrDefaultAsync();

        bool canRequestCageChange = booking.StatusKey is "reserved" or "active";
        List<HotelCageChangeOptionViewModel> availableCages = canRequestCageChange && pendingRequest == null
            ? await GetAvailableCageChangeOptionsAsync(booking, layout.Customer.MembershipTier)
            : [];

        return View(new HotelBookingDetailPageViewModel
        {
            User = layout.User,
            Customer = layout.Customer,
            ActiveNav = layout.ActiveNav,
            Booking = booking,
            CanRequestCageChange = canRequestCageChange,
            PendingCageChangeRequest = pendingRequest,
            AvailableCages = availableCages
        });
    }

    [HttpGet]
    public async Task<IActionResult> AvailableCages(int roomTypeId, DateTime checkInDate, DateTime checkOutDate)
    {
        if (roomTypeId <= 0 || checkOutDate <= checkInDate || checkInDate < DateTime.Now.AddMinutes(-1))
        {
            return BadRequest(new { success = false, message = "Khoảng thời gian hoặc loại phòng không hợp lệ." });
        }

        var roomTypeExists = await _context.RoomTypes
            .AsNoTracking()
            .AnyAsync(roomType => roomType.RoomTypeId == roomTypeId &&
                                  roomType.Status &&
                                  HotelRoomTypeCatalog.Codes.Contains(roomType.Code));
        if (!roomTypeExists)
        {
            return BadRequest(new { success = false, message = "Loại phòng không còn nhận đặt." });
        }

        var conflictingCageIds = await _context.HotelBookings
            .AsNoTracking()
            .Where(booking => BlockingStatuses.Contains(booking.Status) &&
                              booking.CheckInDate < checkOutDate &&
                              (!booking.CheckOutDate.HasValue || booking.CheckOutDate.Value > checkInDate))
            .Select(booking => booking.CageId)
            .Distinct()
            .ToListAsync();

        var availableCages = await _context.Cages
            .AsNoTracking()
            .Where(cage => cage.RoomTypeId == roomTypeId &&
                           cage.Status == "Trống" &&
                           !conflictingCageIds.Contains(cage.CageId))
            .OrderBy(cage => cage.CageId)
            .Select(cage => new { cageId = cage.CageId })
            .ToListAsync();

        return Json(new { success = true, cages = availableCages });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book([FromForm] HotelBookingRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BookingError(GetModelStateErrorMessage());
        }

        var customer = await GetCurrentCustomerAsync();
        if (customer == null)
        {
            return RedirectToAction("Login", "Account", new { area = "Customer" });
        }

        var petId = request.PetId!.Value;
        var roomTypeId = request.RoomTypeId!.Value;
        var requestedCageId = request.CageId.Trim().ToUpperInvariant();
        var checkIn = request.CheckInDate!.Value;
        var checkOut = request.CheckOutDate!.Value;
        var stayDays = Math.Max(1, (int)Math.Ceiling((checkOut - checkIn).TotalHours / 24d));

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var pet = await _context.Pets
                .FirstOrDefaultAsync(p => p.PetId == petId && p.CustomerId == customer.CustomerId);

            if (pet == null)
            {
                return BookingError("Không tìm thấy thú cưng hoặc thú cưng không thuộc tài khoản của bạn.");
            }

            if (!string.Equals(pet.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return BookingError("Hồ sơ thú cưng đã chọn không còn hoạt động.");
            }

            if (pet.Weight <= 0)
            {
                return BookingError(
                    $"Hồ sơ của {pet.Name} chưa có cân nặng hợp lệ. " +
                    "Vui lòng cập nhật hồ sơ thú cưng trước khi đặt chuồng.");
            }

            var roomType = await _context.RoomTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.RoomTypeId == roomTypeId &&
                                          r.Status &&
                                          HotelRoomTypeCatalog.Codes.Contains(r.Code));

            if (roomType == null)
            {
                return BookingError("Loại phòng đã chọn hiện không còn hoạt động.");
            }

            var foodProductSku = request.FoodProductSku.Trim();
            var foodProduct = await _context.Products
                .AsNoTracking()
                .Include(product => product.Category)
                .FirstOrDefaultAsync(product =>
                    product.Sku == foodProductSku &&
                    !product.IsDeleted &&
                    product.Stock > 0 &&
                    product.Unit == HotelFoodCatalog.DailyUnit &&
                    product.Category != null &&
                    !product.Category.IsDeleted &&
                    product.Category.Code == HotelFoodCatalog.CategoryCode);

            if (foodProduct == null)
            {
                return BookingError("Gói thức ăn không còn được cung cấp từ kho cửa hàng.");
            }

            if (!HotelFoodCatalog.IsSpeciesCompatible(foodProduct.AnimalType, pet.Species))
            {
                return BookingError("Gói thức ăn không phù hợp với loài của thú cưng.");
            }

            if (foodProduct.Price <= 0)
            {
                return BookingError("Gói thức ăn chưa có giá bán hợp lệ.");
            }

            var foodQuote = HotelFoodPricing.Calculate(foodProduct.Price, pet.Weight, stayDays);

            var reservedFoodUnits = await _context.HotelBookingFoodPlans
                .Where(plan => plan.ProductSku == foodProduct.Sku &&
                               plan.InventoryQuantityDeducted == 0 &&
                               BlockingStatuses.Contains(plan.HotelBooking.Status))
                .SumAsync(plan => (int?)plan.ChargeableDays) ?? 0;
            var availableFoodUnits = Math.Max(0, foodProduct.Stock - reservedFoodUnits);
            if (availableFoodUnits < foodQuote.InventoryUnits)
            {
                return BookingError(
                    $"{foodProduct.Name} chỉ còn {availableFoodUnits} suất chuẩn, " +
                    $"không đủ {foodQuote.InventoryUnits} suất cho {stayDays} ngày ({foodQuote.WeightBand}).");
            }

            var petHasConflict = await _context.HotelBookings.AnyAsync(b =>
                b.PetId == petId &&
                BlockingStatuses.Contains(b.Status) &&
                b.CheckInDate < checkOut &&
                (!b.CheckOutDate.HasValue || b.CheckOutDate.Value > checkIn));

            if (petHasConflict)
            {
                return BookingError($"{pet.Name} đã có lịch lưu trú trùng với khoảng ngày bạn chọn.");
            }

            var conflictingCageIds = await _context.HotelBookings
                .Where(b =>
                    BlockingStatuses.Contains(b.Status) &&
                    b.CheckInDate < checkOut &&
                    (!b.CheckOutDate.HasValue || b.CheckOutDate.Value > checkIn))
                .Select(b => b.CageId)
                .Distinct()
                .ToListAsync();

            var cage = await _context.Cages
                .AsNoTracking()
                .Where(c =>
                    c.CageId == requestedCageId &&
                    c.RoomTypeId == roomTypeId &&
                    c.Status == "Trống" &&
                    !conflictingCageIds.Contains(c.CageId))
                .FirstOrDefaultAsync();

            if (cage == null)
            {
                return BookingError("Chuồng đã chọn không còn trống trong khoảng thời gian này. Vui lòng chọn lại.");
            }

            var subtotal = roomType.DailyPrice * stayDays;
            var discountRate = ResolveDiscountRate(customer.MembershipTier);
            var discount = decimal.Round(subtotal * discountRate, 0, MidpointRounding.AwayFromZero);
            var foodPricePerDay = foodQuote.PricePerDay;
            var foodTotal = foodQuote.TotalAmount;
            var finalAmount = subtotal - discount + foodTotal;

            await _inventoryBatchService.DeductStockFIFO(foodProduct.Sku, foodQuote.InventoryUnits);

            var booking = new HotelBooking
            {
                CageId = cage.CageId,
                PetId = pet.PetId,
                CustomerId = customer.CustomerId,
                CheckInDate = checkIn,
                CheckOutDate = checkOut,
                ScheduledCheckInDate = checkIn,
                ScheduledCheckOutDate = checkOut,
                StayDays = stayDays,
                BaseDailyPrice = roomType.DailyPrice,
                Subtotal = subtotal,
                Discount = discount,
                FinalAmount = finalAmount,
                EarnedPoints = 0,
                Status = "Đã đặt"
            };

            _context.HotelBookings.Add(booking);
            _context.HotelBookingFoodPlans.Add(new HotelBookingFoodPlan
            {
                HotelBooking = booking,
                ProductSku = foodProduct.Sku,
                PlanType = "HotelProduct",
                FoodNameSnapshot = foodProduct.Name,
                ProductUnitSnapshot = foodProduct.Unit,
                BasePricePerDaySnapshot = foodQuote.BasePricePerDay,
                PetWeightSnapshot = foodQuote.PetWeightKg,
                PortionMultiplierSnapshot = foodQuote.PortionMultiplier,
                PricePerDaySnapshot = foodPricePerDay,
                PortionGrams = 0,
                MealsPerDay = 0,
                FeedingInstructions = null,
                AllergyNotes = string.IsNullOrWhiteSpace(request.AllergyNotes) ? null : request.AllergyNotes.Trim(),
                ChargeableDays = stayDays,
                InventoryQuantityDeducted = foodQuote.InventoryUnits,
                TotalAmount = foodTotal,
                CreatedAt = DateTime.Now
            });
            _context.PetBioTimelines.Add(new PetBioTimeline
            {
                PetId = pet.PetId,
                HotelBooking = booking,
                Date = DateTime.Now,
                Title = "Đặt chuồng lưu trú",
                Type = "HotelBookingCreated",
                Description = $"Khách hàng đặt chuồng {cage.CageId} từ {checkIn:dd/MM/yyyy HH:mm} đến {checkOut:dd/MM/yyyy HH:mm}; " +
                    $"gói ăn {foodProduct.Name} ({foodProduct.Sku}) {foodPricePerDay:N0}đ/ngày, " +
                    $"tạm tính theo cân nặng hồ sơ {foodQuote.PetWeightKg:0.##}kg, hệ số {foodQuote.PortionMultiplier:0.##} ({foodQuote.WeightBand}). " +
                    "Giá và khẩu phần cuối cùng được xác nhận khi tiếp nhận."
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await _hotelEmailService.SendBookingCreatedAsync(
                customer.Email,
                customer.FullName,
                booking.HotelBookingId,
                pet.Name,
                cage.CageId,
                roomType.Type,
                checkIn,
                checkOut,
                finalAmount);

            TempData["SuccessMessage"] =
                $"Đặt phòng thành công cho {pet.Name}. Chuồng dự kiến: {cage.CageId}.";
            return RedirectToAction(nameof(Index));
        }
        catch (ManagePetStore.Exceptions.ServiceException ex)
        {
            await transaction.RollbackAsync();
            return BookingError(ex.Message);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(
                ex,
                "Không thể đặt Hotel online cho CustomerId {CustomerId}, PetId {PetId}.",
                customer.CustomerId,
                petId);
            return BookingError("Không thể hoàn tất đặt phòng do lỗi hệ thống. Vui lòng thử lại.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? searchTerm, string statusFilter = "all", int page = 1)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null)
        {
            return RedirectToAction("Login", "Account", new { area = "Customer" });
        }

        var booking = await _context.HotelBookings
            .Include(b => b.Pet)
            .Include(b => b.FoodPlan)
            .FirstOrDefaultAsync(b =>
                b.HotelBookingId == id &&
                b.CustomerId == customer.CustomerId);

        if (booking == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy lịch đặt phòng hoặc bạn không có quyền hủy.";
            return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
        }

        if (!string.Equals(booking.Status, "Đã đặt", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Chỉ có thể hủy lịch đang ở trạng thái Đã đặt.";
            return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
        }

        var scheduledCheckIn = booking.ScheduledCheckInDate ?? booking.CheckInDate;
        if (scheduledCheckIn <= DateTime.Now.AddHours(1))
        {
            TempData["ErrorMessage"] = "Chỉ có thể hủy online trước giờ nhận phòng ít nhất 1 giờ. Vui lòng liên hệ cửa hàng để được hỗ trợ.";
            return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            if (booking.FoodPlan?.ProductSku != null && booking.FoodPlan.InventoryQuantityDeducted > 0)
            {
                await _inventoryBatchService.RestockToBatches(
                    booking.FoodPlan.ProductSku,
                    booking.FoodPlan.InventoryQuantityDeducted);
                booking.FoodPlan.InventoryQuantityDeducted = 0;
            }

            booking.Status = "Đã hủy";
            _context.PetBioTimelines.Add(new PetBioTimeline
            {
                PetId = booking.PetId,
                HotelBookingId = booking.HotelBookingId,
                Date = DateTime.Now,
                Title = "Hủy lịch lưu trú",
                Type = "HotelBookingCancelled",
                Description = "Khách hàng đã hủy lịch đặt phòng qua hệ thống; suất ăn đã giữ được hoàn lại kho."
            });
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (ManagePetStore.Exceptions.ServiceException ex)
        {
            await transaction.RollbackAsync();
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
        }

        TempData["SuccessMessage"] = $"Đã hủy lịch đặt chuồng của {booking.Pet.Name}.";
        return RedirectToAction(nameof(Index), new { searchTerm, statusFilter, page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestCageChange(int id, string targetCageId, string reason)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null)
        {
            return RedirectToAction("Login", "Account", new { area = "Customer" });
        }

        targetCageId = targetCageId?.Trim().ToUpperInvariant() ?? string.Empty;
        reason = reason?.Trim() ?? string.Empty;
        if (targetCageId.Length is < 1 or > 20 || reason.Length is < 10 or > 500)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn chuồng đích và nhập lý do từ 10 đến 500 ký tự.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var booking = await _context.HotelBookings
                .Include(item => item.Cage).ThenInclude(cage => cage.RoomType)
                .Include(item => item.CheckoutStatement)
                .FirstOrDefaultAsync(item => item.HotelBookingId == id && item.CustomerId == customer.CustomerId);
            if (booking == null || ResolveStatusKey(booking.Status) is not ("reserved" or "active"))
            {
                TempData["ErrorMessage"] = "Booking không còn đủ điều kiện gửi yêu cầu đổi chuồng.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (booking.CheckoutStatement != null)
            {
                TempData["ErrorMessage"] = "Chi phí booking đã được chốt, không thể gửi thêm yêu cầu đổi chuồng.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.Equals(booking.CageId, targetCageId, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Pet đang được xếp tại chuồng này.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (await _context.HotelCageChangeRequests.AnyAsync(item =>
                    item.HotelBookingId == id && item.Status == "Pending"))
            {
                TempData["ErrorMessage"] = "Booking đang có một yêu cầu đổi chuồng chờ xử lý.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var targetCage = await _context.Cages
                .Include(cage => cage.RoomType)
                .FirstOrDefaultAsync(cage => cage.CageId == targetCageId &&
                                             cage.Status == "Trống" &&
                                             cage.RoomType.Status &&
                                             HotelRoomTypeCatalog.Codes.Contains(cage.RoomType.Code));
            if (targetCage == null || await HasCageConflictAsync(booking, targetCageId))
            {
                TempData["ErrorMessage"] = "Chuồng đích không còn khả dụng trong thời gian lưu trú.";
                return RedirectToAction(nameof(Details), new { id });
            }

            int remainingDays = ResolveRemainingChargeDays(booking);
            decimal discountRate = ResolveDiscountRate(customer.MembershipTier);
            decimal estimatedDifference = decimal.Round(
                (targetCage.RoomType.DailyPrice - booking.BaseDailyPrice) * remainingDays * (1 - discountRate),
                0,
                MidpointRounding.AwayFromZero);

            _context.HotelCageChangeRequests.Add(new HotelCageChangeRequest
            {
                HotelBookingId = booking.HotelBookingId,
                CustomerId = customer.CustomerId,
                SourceCageId = booking.CageId,
                TargetCageId = targetCage.CageId,
                Reason = reason,
                Status = "Pending",
                RemainingDaysSnapshot = remainingDays,
                SourceDailyPriceSnapshot = booking.BaseDailyPrice,
                TargetDailyPriceSnapshot = targetCage.RoomType.DailyPrice,
                PriceDifferenceSnapshot = estimatedDifference,
                RequestedAt = DateTime.Now
            });
            _context.PetBioTimelines.Add(new PetBioTimeline
            {
                PetId = booking.PetId,
                HotelBookingId = booking.HotelBookingId,
                Date = DateTime.Now,
                Title = "Yêu cầu đổi chuồng",
                Type = "CageChangeRequested",
                Description = $"Khách hàng yêu cầu đổi từ chuồng {booking.CageId} sang {targetCage.CageId}. Lý do: {reason}. Chênh lệch dự kiến: {estimatedDifference:N0}đ."
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["SuccessMessage"] = $"Đã gửi yêu cầu đổi sang chuồng {targetCage.CageId}. Nhân viên sẽ kiểm tra và phản hồi.";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Cannot create cage change request for HotelBooking {BookingId}.", id);
            TempData["ErrorMessage"] = "Không thể gửi yêu cầu đổi chuồng lúc này.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private IActionResult BookingError(string message)
    {
        TempData["HotelError"] = message;
        return RedirectToAction("Index", "Home", new { area = "", hotel = "book" });
    }

    private async Task<ManagePetStore.Models.Customer?> GetCurrentCustomerAsync()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return null;
        }

        return await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
    }

    private async Task<CustomerSidebarViewModel?> BuildSidebarViewModelAsync(string activeNav)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return null;
        }

        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Customer)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user?.Customer == null)
        {
            return null;
        }

        return new CustomerSidebarViewModel
        {
            User = user,
            Customer = user.Customer,
            ActiveNav = activeNav
        };
    }

    private string GetModelStateErrorMessage()
    {
        return ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
            ?? "Thông tin đặt phòng không hợp lệ.";
    }

    private static decimal ResolveDiscountRate(string? membershipTier)
    {
        return membershipTier?.Trim().ToLowerInvariant() switch
        {
            "gold" or "vàng" => 0.10m,
            "silver" or "bạc" => 0.05m,
            _ => 0m
        };
    }

    private async Task<List<HotelCageChangeOptionViewModel>> GetAvailableCageChangeOptionsAsync(
        HotelBookingHistoryDetailViewModel booking,
        string membershipTier)
    {
        var intervalStart = booking.StatusKey == "active" ? DateTime.Now : booking.CheckInDate;
        var intervalEnd = booking.ScheduledCheckOutDate
            ?? booking.CheckOutDate
            ?? booking.CheckInDate.AddDays(Math.Max(booking.StayDays, 1));
        var conflictingCageIds = await _context.HotelBookings
            .AsNoTracking()
            .Where(item => item.HotelBookingId != booking.HotelBookingId &&
                           BlockingStatuses.Contains(item.Status) &&
                           item.CheckInDate < intervalEnd &&
                           (!item.CheckOutDate.HasValue || item.CheckOutDate.Value > intervalStart))
            .Select(item => item.CageId)
            .Distinct()
            .ToListAsync();
        int remainingDays = booking.StatusKey == "reserved"
            ? Math.Max(booking.StayDays, 1)
            : Math.Max(1, (int)Math.Ceiling((intervalEnd - DateTime.Now).TotalHours / 24d));
        decimal discountRate = ResolveDiscountRate(membershipTier);

        var cages = await _context.Cages
            .AsNoTracking()
            .Where(cage => cage.CageId != booking.CageId &&
                           cage.Status == "Trống" &&
                           cage.RoomType.Status &&
                           HotelRoomTypeCatalog.Codes.Contains(cage.RoomType.Code) &&
                           !conflictingCageIds.Contains(cage.CageId))
            .OrderBy(cage => cage.RoomType.DailyPrice)
            .ThenBy(cage => cage.CageId)
            .Select(cage => new
            {
                CageId = cage.CageId,
                RoomTypeName = cage.RoomType.Type,
                RoomTypeCode = cage.RoomType.Code,
                Size = cage.RoomType.Size,
                DailyPrice = cage.RoomType.DailyPrice
            })
            .ToListAsync();

        return cages.Select(cage => new HotelCageChangeOptionViewModel
        {
            CageId = cage.CageId,
            RoomTypeName = cage.RoomTypeName,
            RoomTypeCode = cage.RoomTypeCode,
            Size = cage.Size,
            DailyPrice = cage.DailyPrice,
            EstimatedPriceDifference = decimal.Round(
                (cage.DailyPrice - booking.BaseDailyPrice) * remainingDays * (1 - discountRate),
                0,
                MidpointRounding.AwayFromZero)
        }).ToList();
    }

    private async Task<bool> HasCageConflictAsync(HotelBooking booking, string targetCageId)
    {
        var intervalStart = ResolveStatusKey(booking.Status) == "active" ? DateTime.Now : booking.CheckInDate;
        var intervalEnd = booking.ScheduledCheckOutDate
            ?? booking.CheckOutDate
            ?? booking.CheckInDate.AddDays(Math.Max(booking.StayDays, 1));
        return await _context.HotelBookings.AnyAsync(item =>
            item.HotelBookingId != booking.HotelBookingId &&
            item.CageId == targetCageId &&
            BlockingStatuses.Contains(item.Status) &&
            item.CheckInDate < intervalEnd &&
            (!item.CheckOutDate.HasValue || item.CheckOutDate.Value > intervalStart));
    }

    private static int ResolveRemainingChargeDays(HotelBooking booking)
    {
        if (ResolveStatusKey(booking.Status) == "reserved")
        {
            return Math.Max(booking.StayDays, 1);
        }

        var expectedCheckout = booking.ScheduledCheckOutDate
            ?? booking.CheckOutDate
            ?? DateTime.Now.AddDays(1);
        return Math.Max(1, (int)Math.Ceiling((expectedCheckout - DateTime.Now).TotalHours / 24d));
    }

    private static HotelBookingListItemViewModel MapToListItem(HotelBooking booking)
    {
        var statusKey = ResolveStatusKey(booking.Status);

        bool canCancel = statusKey == "reserved" &&
            (booking.ScheduledCheckInDate ?? booking.CheckInDate) > DateTime.Now.AddHours(1);

        return new HotelBookingListItemViewModel
        {
            HotelBookingId = booking.HotelBookingId,
            PetName = booking.Pet.Name,
            CageId = booking.CageId,
            RoomTypeName = booking.Cage.RoomType.Type,
            CheckInDate = booking.ScheduledCheckInDate ?? booking.CheckInDate,
            CheckOutDate = booking.ScheduledCheckOutDate
                ?? booking.CheckOutDate
                ?? booking.CheckInDate.AddDays(booking.StayDays),
            StayDays = booking.StayDays,
            FinalAmount = booking.FinalAmount,
            Status = booking.Status,
            StatusKey = statusKey,
            CanCancel = canCancel,
            ShowCannotCancelOnline = statusKey == "reserved" && !canCancel
        };
    }

    private static string ResolveStatusKey(string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "đã đặt" => "reserved",
            "active" or "đang ở" => "active",
            "đã trả" => "completed",
            "đã hủy" or "cancelled" or "từ chối tiếp nhận" => "cancelled",
            _ => "other"
        };
    }
}
