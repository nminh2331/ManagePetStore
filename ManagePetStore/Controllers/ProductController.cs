using ManagePetStore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Controllers;

public class ProductController : Controller
{
    private readonly PetStoreManagementContext _context;

    public ProductController(PetStoreManagementContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToAction("Index", "Home");
        }

        ProductDetailViewModel? model = null;  // Khai báo biến model có kiểu dữ liệu là ProductDetailViewModel

        try
        {
            if (id.StartsWith("SPA-SVC-", StringComparison.OrdinalIgnoreCase))
            {
                var idString = id.Substring(8); //Cắt bỏ 8 ký tự đầu tiên (SPA-SVC-) để lấy phần số ở đuôi (ví dụ 001).
                if (int.TryParse(idString, out int serviceId))  // Ép phần đuôi đó thành số nguyên (int).
                {
                    var spaService = await _context.SpaServices.FirstOrDefaultAsync(s => s.ServiceId == serviceId);
                    if (spaService != null)
                    {
                        model = MapFromSpaService(spaService);  // chuyển đổi dữ liệu thô từ DB thành ProductDetailViewModel để UI đọc được.
                        
                        // Query extra info for Spa service booking form
                        Customer? customerObj = null;
                        List<Pet> petsList = new List<Pet>();
                        if (User.Identity?.IsAuthenticated == true)
                        {
                            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
                            {
                                customerObj = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
                                if (customerObj != null)
                                {
                                    petsList = await _context.Pets
                                        .Where(p => p.CustomerId == customerObj.CustomerId && p.Status == "Active")
                                        .ToListAsync();

                                    var petIds = petsList.Select(p => p.PetId).ToList();
                                    var petsWithRecords = await _context.MedicalRecords
                                        .Where(mr => petIds.Contains(mr.PetId))
                                        .Select(mr => mr.PetId)
                                        .Distinct()
                                        .ToListAsync();
                                    ViewBag.PetIdsWithRecords = new HashSet<int>(petsWithRecords);
                                }
                            }
                        }
                        
                        var groomersList = await _context.Users
                            .Include(u => u.Role)
                            .Where(u => u.Role.RoleName == "service" && u.Status == "Active")
                            .ToListAsync();
                            
                        ViewBag.LoggedInCustomer = customerObj;
                        ViewBag.CustomerPets = petsList;
                        ViewBag.ActiveGroomers = groomersList;
                    }
                }
            }
            else
            {
                var product = await _context.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Sku == id);  
                //Eager Loading để lấy kèm thông tin Danh mục (tương tự trang chủ).
                if (product != null)
                {
                    model = MapFromProduct(product);
                }
            }
        }
        catch
        {
            // Fallback to static demo data below.
        }

        model ??= GetStaticProduct(id) ?? GetStaticProduct("RC-MBC-001")!;

        return View(model);
    }

    private static ProductDetailViewModel MapFromSpaService(SpaService service)
    {
        var originalPrice = Math.Round(service.Price * 1.11m, 0);
        var discount = (int)Math.Round((1 - service.Price / originalPrice) * 100);

        return new ProductDetailViewModel
        {
            Sku = $"SPA-SVC-{service.ServiceId:D3}",
            Brand = "DỊCH VỤ SPA",
            Name = service.Name,
            FullTitle = $"{service.Name} - Liệu trình chăm sóc chuyên nghiệp",
            Price = service.Price,
            OriginalPrice = originalPrice,
            DiscountPercent = discount,
            Savings = originalPrice - service.Price,
            Rating = 4.9,
            ReviewCount = 35,
            SoldCount = "100+",
            Description = $"Dịch vụ {service.Name} chất lượng cao giúp thú cưng sạch sẽ, khỏe mạnh và thoải mái. Liệu trình thực hiện trong {service.DurationMinutes} phút bởi các chuyên viên spa tay nghề cao.",
            Stock = 999, // Spa services always have virtual stock
            InStock = true,
            Images =
            [
                "https://images.unsplash.com/photo-1516734212186-a967f81ad0d7?w=600&h=600&fit=crop",
                "https://images.unsplash.com/photo-1558788353-f76d92427f16?w=600&h=600&fit=crop",
                "https://images.unsplash.com/photo-1548199973-03cce0bbc87b?w=600&h=600&fit=crop"
            ],
            Features =
            [
                $"Thời gian thực hiện: {service.DurationMinutes} phút",
                "Chăm sóc tận tình chuẩn 5 sao",
                "Chuyên viên giàu kinh nghiệm",
                "Sử dụng sữa tắm, mỹ phẩm cao cấp an toàn"
            ]
        };
    }


    private static ProductDetailViewModel MapFromProduct(Product product)
    {
        var originalPrice = Math.Round(product.Price * 1.11m, 0);
        var discount = (int)Math.Round((1 - product.Price / originalPrice) * 100);

        return new ProductDetailViewModel
        {
            Sku = product.Sku,
            Brand = product.Category?.Name.ToUpperInvariant() ?? "PETSTORE",
            Name = product.Name,
            FullTitle = product.Name,
            Price = product.Price,
            OriginalPrice = originalPrice,
            DiscountPercent = discount,
            Savings = originalPrice - product.Price,
            Rating = 4.8,
            ReviewCount = 124,
            SoldCount = "1.2k+",
            Description = "Thức ăn cao cấp được nghiên cứu đặc biệt cho mèo mẹ và mèo con, cung cấp đầy đủ dưỡng chất thiết yếu giúp mèo con phát triển khỏe mạnh.",
            Stock = product.Stock,
            InStock = product.Stock > 0,
            Images =
            [
                string.IsNullOrEmpty(product.ImageUrl)
                    ? "https://images.unsplash.com/photo-1589924691995-400dc9ecc119?w=600&h=600&fit=crop"
                    : product.ImageUrl,
                "https://images.unsplash.com/photo-1516734212186-a967f81ad0d7?w=600&h=600&fit=crop",
                "https://images.unsplash.com/photo-1450778869180-41d0601e046e?w=600&h=600&fit=crop"
            ],
            Features =
            [
                "Hỗ trợ hệ miễn dịch",
                "Dễ dàng cai sữa",
                "Tăng cường sức khỏe hệ tiêu hóa",
                "Giàu DHA cho phát triển trí não"
            ]
        };
    }

    private static ProductDetailViewModel? GetStaticProduct(string sku)
    {
        var products = new Dictionary<string, ProductDetailViewModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["RC-MBC-001"] = new()
            {
                Sku = "RC-MBC-001",
                Brand = "ROYAL CANIN",
                Name = "Royal Canin Mother & Babycat",
                FullTitle = "Royal Canin Mother & Babycat - Thức ăn cho mèo mẹ và mèo con",
                Price = 350000,
                OriginalPrice = 388000,
                DiscountPercent = 10,
                Savings = 38000,
                Rating = 4.8,
                ReviewCount = 124,
                SoldCount = "1.2k+",
                Description = "Thức ăn cao cấp được nghiên cứu đặc biệt cho mèo mẹ và mèo con, cung cấp đầy đủ dưỡng chất thiết yếu giúp mèo con phát triển khỏe mạnh trong giai đoạn đầu đời quan trọng nhất.",
                Stock = 45,
                InStock = true,
                Images =
                [
                    "https://images.unsplash.com/photo-1589924691995-400dc9ecc119?w=600&h=600&fit=crop",
                    "https://images.unsplash.com/photo-1516734212186-a967f81ad0d7?w=600&h=600&fit=crop",
                    "https://images.unsplash.com/photo-1450778869180-41d0601e046e?w=600&h=600&fit=crop"
                ],
                Features =
                [
                    "Hỗ trợ hệ miễn dịch",
                    "Dễ dàng cai sữa",
                    "Tăng cường sức khỏe hệ tiêu hóa",
                    "Giàu DHA cho phát triển trí não"
                ]
            },
            ["MN-CAT-5L"] = new()
            {
                Sku = "MN-CAT-5L",
                Brand = "MANEKI NEKO",
                Name = "Cát vệ sinh Maneki Neko 5L",
                FullTitle = "Cát vệ sinh Maneki Neko 5L - Khử mùi hiệu quả",
                Price = 89000,
                OriginalPrice = 99000,
                DiscountPercent = 10,
                Savings = 10000,
                Rating = 4.6,
                ReviewCount = 89,
                SoldCount = "800+",
                Description = "Cát vệ sinh cao cấp với khả năng khử mùi vượt trội, vón cục tốt và an toàn cho mèo cưng.",
                Stock = 120,
                InStock = true,
                Images =
                [
                    "https://images.unsplash.com/photo-1516734212186-a967f81ad0d7?w=600&h=600&fit=crop",
                    "https://images.unsplash.com/photo-1589924691995-400dc9ecc119?w=600&h=600&fit=crop"
                ],
                Features = ["Khử mùi 24h", "Vón cục chắc", "Không bụi", "An toàn cho mèo"]
            },
            ["JD-SHAMPOO"] = new()
            {
                Sku = "JD-SHAMPOO",
                Brand = "JOYCE & DOLLS",
                Name = "Sữa tắm Joyce & Dolls 400ml",
                FullTitle = "Sữa tắm Joyce & Dolls 400ml - Dịu nhẹ cho da lông",
                Price = 125000,
                OriginalPrice = 125000,
                DiscountPercent = 0,
                Savings = 0,
                Rating = 4.9,
                ReviewCount = 56,
                SoldCount = "350+",
                Description = "Sữa tắm dịu nhẹ chiết xuất thảo mộc, giúp lông mềm mượt và da khỏe mạnh.",
                Stock = 60,
                InStock = true,
                Images =
                [
                    "https://images.unsplash.com/photo-1558788353-f76d92427f16?w=600&h=600&fit=crop",
                    "https://images.unsplash.com/photo-1516734212186-a967f81ad0d7?w=600&h=600&fit=crop"
                ],
                Features = ["pH cân bằng", "Không gây kích ứng", "Hương thảo mộc", "Dưỡng lông mềm"]
            },
            ["BONE-TET-5"] = new()
            {
                Sku = "BONE-TET-5",
                Brand = "PETSTORE",
                Name = "Xương gặm cho chó 5 cây",
                FullTitle = "Xương gặm cho chó 5 cây - Giúp sạch răng",
                Price = 45000,
                OriginalPrice = 50000,
                DiscountPercent = 10,
                Savings = 5000,
                Rating = 4.5,
                ReviewCount = 32,
                SoldCount = "200+",
                Description = "Xương gặm tự nhiên giúp làm sạch răng và massage nướu cho chó cưng.",
                Stock = 200,
                InStock = true,
                Images =
                [
                    "/images/dog-bone-chew.png"
                ],
                Features = ["Làm sạch răng", "Massage nướu", "100% tự nhiên", "Không chất bảo quản"]
            }
        };

        return products.GetValueOrDefault(sku);
    }

    [HttpGet]
    public async Task<IActionResult> GetGroomerBookedSlots(int groomerId, string date)
    {
        if (!DateTime.TryParse(date, out DateTime parsedDate))
        {
            parsedDate = DateTime.Today;
        }

        var bookedSlots = await _context.SpaBookings
            .Where(b => b.GroomerId == groomerId && b.DateTime.Date == parsedDate.Date && b.SpaStatus != "Cancelled")
            .Select(b => b.DateTime.ToString("HH:mm"))
            .ToListAsync();

        return Json(bookedSlots);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BookSpaService(
        string sku,
        int? petId,
        string? newPetName,
        string? newPetSpecies,
        string? newPetBreed,
        string? newPetAge,
        decimal? newPetWeight,
        int? groomerId,
        string bookingDate,
        string bookingTime,
        string? note)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            TempData["ErrorMessage"] = "Bạn phải đăng nhập tài khoản mới có thể đặt lịch dịch vụ.";
            return RedirectToAction("Login", "Account", new { area = "Customer", returnUrl = Url.Action("Details", "Product", new { id = sku }) });
        }

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            TempData["ErrorMessage"] = "Không tìm thấy thông tin đăng nhập.";
            return RedirectToAction("Index", "Home");
        }

        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
        if (customer == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy thông tin khách hàng.";
            return RedirectToAction("Index", "Home");
        }

        // 1. Resolve Pet
        Pet? pet = null;
        if (petId.HasValue && petId.Value > 0)
        {
            pet = await _context.Pets.FirstOrDefaultAsync(p => p.PetId == petId.Value && p.CustomerId == customer.CustomerId);
            if (pet == null)
            {
                TempData["ErrorMessage"] = "Thú cưng được chọn không hợp lệ.";
                return RedirectToAction("Details", new { id = sku });
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(newPetName))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập tên thú cưng mới.";
                return RedirectToAction("Details", new { id = sku });
            }

            if (newPetWeight.HasValue && (newPetWeight.Value <= 0 || newPetWeight.Value > 200m))
            {
                TempData["ErrorMessage"] = "Cân nặng thú cưng phải lớn hơn 0 và không vượt quá 200 kg.";
                return RedirectToAction("Details", new { id = sku });
            }

            pet = new Pet
            {
                CustomerId = customer.CustomerId,
                Name = newPetName.Trim(),
                Species = newPetSpecies ?? "Chó",
                Breed = newPetBreed?.Trim() ?? "Không rõ",
                Age = newPetAge?.Trim() ?? "Chưa rõ",
                Weight = newPetWeight.HasValue && newPetWeight.Value > 0 ? newPetWeight.Value : 4.5m,
                Status = "Active"
            };
            _context.Pets.Add(pet);
            await _context.SaveChangesAsync();
        }

        // 2. Resolve Service
        if (string.IsNullOrWhiteSpace(sku) || !sku.StartsWith("SPA-SVC-", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Mã dịch vụ không hợp lệ.";
            return RedirectToAction("Index", "Home");
        }

        var idString = sku.Substring(8);
        if (!int.TryParse(idString, out int serviceId))
        {
            TempData["ErrorMessage"] = "Mã dịch vụ không hợp lệ.";
            return RedirectToAction("Index", "Home");
        }

        var service = await _context.SpaServices.FirstOrDefaultAsync(s => s.ServiceId == serviceId);
        if (service == null || !service.Active)
        {
            TempData["ErrorMessage"] = "Dịch vụ đã chọn không tồn tại hoặc ngừng hoạt động.";
            return RedirectToAction("Index", "Home");
        }

        // 3. Resolve Groomer
        int targetGroomerId = groomerId ?? 0;
        bool hasPreferredGroomer = targetGroomerId > 0;
        User? preferredGroomer = null;

        if (hasPreferredGroomer)
        {
            preferredGroomer = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == targetGroomerId);
        }

        if (targetGroomerId <= 0 || preferredGroomer == null || preferredGroomer.Status != "Active" || preferredGroomer.Role?.RoleName != "service")
        {
            var defaultGroomer = await _context.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Role.RoleName == "service" && u.Status == "Active")
                ?? await _context.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Role.RoleName == "service")
                ?? await _context.Users.FirstOrDefaultAsync();
            targetGroomerId = defaultGroomer?.UserId ?? 3;
            preferredGroomer = await _context.Users.FindAsync(targetGroomerId);
        }

        // 4. Resolve Date and Time
        if (!DateTime.TryParse($"{bookingDate} {bookingTime}", out DateTime bookingDateTime))
        {
            TempData["ErrorMessage"] = "Ngày hoặc giờ đặt lịch không hợp lệ.";
            return RedirectToAction("Details", new { id = sku });
        }

        // Check if date and time is in the past
        if (bookingDateTime < DateTime.Now)
        {
            TempData["ErrorMessage"] = "Không thể đặt lịch ở thời điểm trong quá khứ.";
            return RedirectToAction("Details", new { id = sku });
        }

        // Limit booking window to 90 days
        if (bookingDateTime > DateTime.Today.AddDays(90))
        {
            TempData["ErrorMessage"] = "Chỉ có thể đặt lịch trước tối đa 90 ngày.";
            return RedirectToAction("Details", new { id = sku });
        }

        // Validate business hours (08:00 - 17:00)
        if (bookingDateTime.Hour < 8 || bookingDateTime.Hour >= 17)
        {
            TempData["ErrorMessage"] = "Thời gian đặt lịch phải nằm trong giờ mở cửa (08:00 - 17:00).";
            return RedirectToAction("Details", new { id = sku });
        }

        // Check if groomer has an overlapping appointment at this slot (Interval Overlap Check - BR-29)
        var bookedSlotsToday = await _context.SpaBookings
            .Include(b => b.Service)
            .Where(b => b.GroomerId == targetGroomerId 
                     && b.DateTime.Date == bookingDateTime.Date 
                     && b.SpaStatus != "Cancelled")
            .ToListAsync();

        bool isOverlap = bookedSlotsToday.Any(b => {
            var existingStart = b.DateTime;
            var existingEnd = b.DateTime.AddMinutes(b.Service?.DurationMinutes ?? 30);
            var newStart = bookingDateTime;
            var newEnd = bookingDateTime.AddMinutes(service.DurationMinutes);
            return newStart < existingEnd && existingStart < newEnd;
        });

        if (isOverlap)
        {
            TempData["ErrorMessage"] = "Kỹ thuật viên đã có ca làm việc ở khung giờ này. Vui lòng chọn khung giờ hoặc kỹ thuật viên khác.";
            return RedirectToAction("Details", new { id = sku });
        }

        // 6. Create Booking & Queue Item
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                var booking = new SpaBooking
                {
                    CustomerId = customer.CustomerId,
                    PetId = pet.PetId,
                    ServiceId = service.ServiceId,
                    GroomerId = targetGroomerId,
                    DateTime = bookingDateTime,
                    Price = service.Price,
                    Status = "Chưa thanh toán",
                    SpaStatus = "|0",
                    Notes = note?.Trim()
                };
                _context.SpaBookings.Add(booking);
                await _context.SaveChangesAsync();

                int countToday = await _context.SpaQueues.CountAsync(q => q.QueueNumber.StartsWith("OL-"));
                string queueNumber = $"OL-{(100 + countToday + 1)}";

                string preferredGroomerLabel = hasPreferredGroomer && preferredGroomer != null
                    ? preferredGroomer.FullName
                    : "Không yêu cầu";

                var ownerLabel = $"{customer.FullName} ({customer.Phone})";
                if (ownerLabel.Length > 100)
                {
                    ownerLabel = ownerLabel.Substring(0, 100);
                }

                var queueItem = new SpaQueue
                {
                    QueueNumber = queueNumber,
                    PetName = pet.Name,
                    OwnerName = ownerLabel,
                    ArrivalTime = bookingDateTime,
                    ServiceDescription = service.Name,
                    Note = $"[NV mong muốn: {preferredGroomerLabel}] " + note?.Trim()
                };
                _context.SpaQueues.Add(queueItem);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Đặt lịch dịch vụ thành công! Lịch hẹn của bạn vào lúc {bookingTime} ngày {bookingDate} đã được ghi nhận.";
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Có lỗi xảy ra trong quá trình đặt lịch. Vui lòng thử lại.";
                return RedirectToAction("Details", new { id = sku });
            }
        }

        return RedirectToAction("Details", new { id = sku });
    }
}
