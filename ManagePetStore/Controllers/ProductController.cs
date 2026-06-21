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

        ProductDetailViewModel? model = null;  // Khai báo bi?n model có ki?u d? li?u là ProductDetailViewModel

        try
        {
            if (id.StartsWith("SPA-SVC-", StringComparison.OrdinalIgnoreCase))
            {
                var idString = id.Substring(8); //C?t b? 8 ký t? d?u tiên (SPA-SVC-) d? l?y ph?n s? ? duôi (ví d? 001).
                if (int.TryParse(idString, out int serviceId))  // Ép ph?n duôi dó thành s? nguyên (int).
                {
                    var spaService = await _context.SpaServices.FirstOrDefaultAsync(s => s.ServiceId == serviceId);
                    if (spaService != null)
                    {
                        model = MapFromSpaService(spaService);  // chuy?n d?i d? li?u thô t? DB thành ProductDetailViewModel d? UI d?c du?c.
                        
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
                //Eager Loading d? l?y kèm thông tin Danh m?c (tuong t? trang ch?).
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
            Brand = "D?CH V? SPA",
            Name = service.Name,
            FullTitle = $"{service.Name} - Li?u trình cham sóc chuyên nghi?p",
            Price = service.Price,
            OriginalPrice = originalPrice,
            DiscountPercent = discount,
            Savings = originalPrice - service.Price,
            Rating = 4.9,
            ReviewCount = 35,
            SoldCount = "100+",
            Description = $"D?ch v? {service.Name} ch?t lu?ng cao giúp thú cung s?ch s?, kh?e m?nh và tho?i mái. Li?u trình th?c hi?n trong {service.DurationMinutes} phút b?i các chuyên viên spa tay ngh? cao.",
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
                $"Th?i gian th?c hi?n: {service.DurationMinutes} phút",
                "Cham sóc t?n tình chu?n 5 sao",
                "Chuyên viên giàu kinh nghi?m",
                "S? d?ng s?a t?m, m? ph?m cao c?p an toàn"
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
            Description = "Th?c an cao c?p du?c nghiên c?u d?c bi?t cho mèo m? và mèo con, cung c?p d?y d? du?ng ch?t thi?t y?u giúp mèo con phát tri?n kh?e m?nh.",
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
                "H? tr? h? mi?n d?ch",
                "D? dàng cai s?a",
                "Tang cu?ng s?c kh?e h? tiêu hóa",
                "Giàu DHA cho phát tri?n trí não"
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
                FullTitle = "Royal Canin Mother & Babycat - Th?c an cho mèo m? và mèo con",
                Price = 350000,
                OriginalPrice = 388000,
                DiscountPercent = 10,
                Savings = 38000,
                Rating = 4.8,
                ReviewCount = 124,
                SoldCount = "1.2k+",
                Description = "Th?c an cao c?p du?c nghiên c?u d?c bi?t cho mèo m? và mèo con, cung c?p d?y d? du?ng ch?t thi?t y?u giúp mèo con phát tri?n kh?e m?nh trong giai do?n d?u d?i quan tr?ng nh?t.",
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
                    "H? tr? h? mi?n d?ch",
                    "D? dàng cai s?a",
                    "Tang cu?ng s?c kh?e h? tiêu hóa",
                    "Giàu DHA cho phát tri?n trí não"
                ]
            },
            ["MN-CAT-5L"] = new()
            {
                Sku = "MN-CAT-5L",
                Brand = "MANEKI NEKO",
                Name = "Cát v? sinh Maneki Neko 5L",
                FullTitle = "Cát v? sinh Maneki Neko 5L - Kh? mùi hi?u qu?",
                Price = 89000,
                OriginalPrice = 99000,
                DiscountPercent = 10,
                Savings = 10000,
                Rating = 4.6,
                ReviewCount = 89,
                SoldCount = "800+",
                Description = "Cát v? sinh cao c?p v?i kh? nang kh? mùi vu?t tr?i, vón c?c t?t và an toàn cho mèo cung.",
                Stock = 120,
                InStock = true,
                Images =
                [
                    "https://images.unsplash.com/photo-1516734212186-a967f81ad0d7?w=600&h=600&fit=crop",
                    "https://images.unsplash.com/photo-1589924691995-400dc9ecc119?w=600&h=600&fit=crop"
                ],
                Features = ["Kh? mùi 24h", "Vón c?c ch?c", "Không b?i", "An toàn cho mèo"]
            },
            ["JD-SHAMPOO"] = new()
            {
                Sku = "JD-SHAMPOO",
                Brand = "JOYCE & DOLLS",
                Name = "S?a t?m Joyce & Dolls 400ml",
                FullTitle = "S?a t?m Joyce & Dolls 400ml - D?u nh? cho da lông",
                Price = 125000,
                OriginalPrice = 125000,
                DiscountPercent = 0,
                Savings = 0,
                Rating = 4.9,
                ReviewCount = 56,
                SoldCount = "350+",
                Description = "S?a t?m d?u nh? chi?t xu?t th?o m?c, giúp lông m?m mu?t và da kh?e m?nh.",
                Stock = 60,
                InStock = true,
                Images =
                [
                    "https://images.unsplash.com/photo-1558788353-f76d92427f16?w=600&h=600&fit=crop",
                    "https://images.unsplash.com/photo-1516734212186-a967f81ad0d7?w=600&h=600&fit=crop"
                ],
                Features = ["pH cân b?ng", "Không gây kích ?ng", "Huong th?o m?c", "Du?ng lông m?m"]
            },
            ["BONE-TET-5"] = new()
            {
                Sku = "BONE-TET-5",
                Brand = "PETSTORE",
                Name = "Xuong g?m cho chó 5 cây",
                FullTitle = "Xuong g?m cho chó 5 cây - Giúp s?ch rang",
                Price = 45000,
                OriginalPrice = 50000,
                DiscountPercent = 10,
                Savings = 5000,
                Rating = 4.5,
                ReviewCount = 32,
                SoldCount = "200+",
                Description = "Xuong g?m t? nhiên giúp làm s?ch rang và massage nu?u cho chó cung.",
                Stock = 200,
                InStock = true,
                Images =
                [
                    "/images/dog-bone-chew.png"
                ],
                Features = ["Làm s?ch rang", "Massage nu?u", "100% t? nhiên", "Không ch?t b?o qu?n"]
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
            TempData["ErrorMessage"] = "B?n ph?i dang nh?p tài kho?n m?i có th? d?t l?ch d?ch v?.";
            return RedirectToAction("Login", "Account", new { area = "Customer", returnUrl = Url.Action("Details", "Product", new { id = sku }) });
        }

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            TempData["ErrorMessage"] = "Không tìm th?y thông tin dang nh?p.";
            return RedirectToAction("Index", "Home");
        }

        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
        if (customer == null)
        {
            TempData["ErrorMessage"] = "Không tìm th?y thông tin khách hàng.";
            return RedirectToAction("Index", "Home");
        }

        // 1. Resolve Pet
        Pet? pet = null;
        if (petId.HasValue && petId.Value > 0)
        {
            pet = await _context.Pets.FirstOrDefaultAsync(p => p.PetId == petId.Value && p.CustomerId == customer.CustomerId);
            if (pet == null)
            {
                TempData["ErrorMessage"] = "Thú cung du?c ch?n không h?p l?.";
                return RedirectToAction("Details", new { id = sku });
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(newPetName))
            {
                TempData["ErrorMessage"] = "Vui lòng nh?p tên thú cung m?i.";
                return RedirectToAction("Details", new { id = sku });
            }

            pet = new Pet
            {
                CustomerId = customer.CustomerId,
                Name = newPetName.Trim(),
                Species = newPetSpecies ?? "Chó",
                Breed = newPetBreed?.Trim() ?? "Không rõ",
                Age = newPetAge?.Trim() ?? "Chua rõ",
                Weight = newPetWeight.HasValue && newPetWeight.Value > 0 ? newPetWeight.Value : 4.5m,
                Status = "Active"
            };
            _context.Pets.Add(pet);
            await _context.SaveChangesAsync();
        }

        // 2. Resolve Service
        if (string.IsNullOrWhiteSpace(sku) || !sku.StartsWith("SPA-SVC-", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Mã d?ch v? không h?p l?.";
            return RedirectToAction("Index", "Home");
        }

        var idString = sku.Substring(8);
        if (!int.TryParse(idString, out int serviceId))
        {
            TempData["ErrorMessage"] = "Mã d?ch v? không h?p l?.";
            return RedirectToAction("Index", "Home");
        }

        var service = await _context.SpaServices.FirstOrDefaultAsync(s => s.ServiceId == serviceId);
        if (service == null || !service.Active)
        {
            TempData["ErrorMessage"] = "D?ch v? dã ch?n không t?n t?i ho?c ng?ng ho?t d?ng.";
            return RedirectToAction("Index", "Home");
        }

        // 3. Resolve Groomer
        int targetGroomerId = groomerId ?? 0;
        bool hasPreferredGroomer = targetGroomerId > 0;
        User? preferredGroomer = null;

        if (hasPreferredGroomer)
        {
            preferredGroomer = await _context.Users.FindAsync(targetGroomerId);
        }

        if (targetGroomerId <= 0 || preferredGroomer == null || preferredGroomer.Status != "Active")
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
            TempData["ErrorMessage"] = "Ngày ho?c gi? d?t l?ch không h?p l?.";
            return RedirectToAction("Details", new { id = sku });
        }

        // Check if date is in the past
        if (bookingDateTime.Date < DateTime.Today)
        {
            TempData["ErrorMessage"] = "Không th? d?t l?ch cho ngày trong quá kh?.";
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
                    Status = "Chua thanh toán",
                    SpaStatus = "|0",
                    Notes = note?.Trim()
                };
                _context.SpaBookings.Add(booking);
                await _context.SaveChangesAsync();

                int countToday = await _context.SpaQueues.CountAsync(q => q.QueueNumber.StartsWith("OL-"));
                string queueNumber = $"OL-{(100 + countToday + 1)}";

                string preferredGroomerLabel = hasPreferredGroomer && preferredGroomer != null
                    ? preferredGroomer.FullName
                    : "Không yêu c?u";

                var queueItem = new SpaQueue
                {
                    QueueNumber = queueNumber,
                    PetName = pet.Name,
                    OwnerName = $"{customer.FullName} ({customer.Phone})",
                    ArrivalTime = bookingDateTime,
                    ServiceDescription = service.Name,
                    Note = $"[NV mong mu?n: {preferredGroomerLabel}] " + note?.Trim()
                };
                _context.SpaQueues.Add(queueItem);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Ð?t l?ch d?ch v? thành công! L?ch h?n c?a b?n vào lúc {bookingTime} ngày {bookingDate} dã du?c ghi nh?n.";
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Có l?i x?y ra trong quá trình d?t l?ch. Vui lòng th? l?i.";
                return RedirectToAction("Details", new { id = sku });
            }
        }

        return RedirectToAction("Details", new { id = sku });
    }
}



