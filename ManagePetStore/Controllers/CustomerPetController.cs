using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using ManagePetStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Controllers;

[Authorize]
[Route("Customer/Pet/{action=Index}/{id?}")]
public class CustomerPetController : Controller
{
    private readonly PetStoreManagementContext _context;
    private readonly IWebHostEnvironment _env;
    private static readonly string[] AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".tiff", ".tif", ".svg"];
    private const long MaxImageBytes = 20L * 1024 * 1024;
    private static readonly Regex WeightPattern = new(@"^\d+(\.\d+)?$", RegexOptions.CultureInvariant);

    public CustomerPetController(PetStoreManagementContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    [HttpGet]

    // Trang danh sách & Chuẩn bị Form Sửa
    public async Task<IActionResult> Index(int? editId = null)
    {
        var page = await BuildPageViewModelAsync();   // Gọi hàm hỗ trợ để gom thông tin User, Khách hàng và danh sách Pets của người đó.
        if (page == null)
        {
            return RedirectToAction("Login", "CustomerAccount");
        }

        if (editId.HasValue)
        {
            var pet = await GetOwnedPetAsync(editId.Value);  //  Nó kiểm tra xem thú cưng có ID đó có thực sự thuộc về khách hàng đang đăng nhập hay không.
            if (pet == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thú cưng hoặc bạn không có quyền truy cập.";
                return RedirectToAction(nameof(Index));
            }

            page.EditPet = MapPetToForm(pet);  // Nếu đúng pet của mình, chuyển data từ DB sang dạng Form.
            page.OpenEditModal = true;
        }

        return View(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // hàm thêm thú cưng mới 
    public async Task<IActionResult> Create(
        string name,
        string species,
        string breed,
        DateTime? dateOfBirth,
        decimal weight,
        string? pathology,
        IFormFile? avatarFile)
    {
        var customerId = await GetCurrentCustomerIdAsync();
        if (customerId == null)
        {
            return RedirectToAction("Login", "CustomerAccount");
        }

        var fieldErrors = CollectPetFormErrors(name, species, breed, dateOfBirth, weight, avatarFile);
        if (fieldErrors.Count > 0)
        {
            TempData["PetFieldErrors"] = JsonSerializer.Serialize(fieldErrors);
            TempData["OpenCreateModal"] = true;
            return RedirectToAction(nameof(Index));
        }

        var imageUrl = await SavePetImageAsync(avatarFile);
        if (avatarFile != null && avatarFile.Length > 0 && imageUrl == null)
        {
            TempData["PetFieldErrors"] = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["avatarFile"] = "Ảnh không hợp lệ. Chỉ chấp nhận JPG, JPEG, PNG, GIF, TIFF, SVG và tối đa 20MB."
            });
            TempData["OpenCreateModal"] = true;
            return RedirectToAction(nameof(Index));
        }

        var pet = new Pet
        {
            CustomerId = customerId.Value,
            Name = name.Trim(),
            Species = species.Trim(),
            Breed = breed.Trim(),
            Weight = weight,
            Age = FormatAge(dateOfBirth!.Value),
            Pathology = string.IsNullOrWhiteSpace(pathology) ? "Khỏe mạnh" : pathology.Trim(),
            ImageUrl = imageUrl ?? GetDefaultPetImage(species),
            Status = "Active"
        };

        _context.Pets.Add(pet);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã thêm hồ sơ bé [{pet.Name}] thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // ham edit 
    public async Task<IActionResult> Edit(
        int petId,
        string name,
        string species,
        string breed,
        DateTime? dateOfBirth,
        decimal weight,
        string? pathology,
        IFormFile? avatarFile)
    {
        var pet = await GetOwnedPetAsync(petId);  // dam bao quyen so huu 
        if (pet == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy thú cưng hoặc bạn không có quyền chỉnh sửa.";
            return RedirectToAction(nameof(Index));
        }

        var fieldErrors = CollectPetFormErrors(name, species, breed, dateOfBirth, weight, avatarFile);
        if (fieldErrors.Count > 0)
        {
            TempData["PetFieldErrors"] = JsonSerializer.Serialize(fieldErrors);
            return RedirectToAction(nameof(Index), new { editId = petId });
        }

        if (avatarFile != null && avatarFile.Length > 0)
        {
            var newImageUrl = await SavePetImageAsync(avatarFile);
            if (newImageUrl == null)
            {
                TempData["PetFieldErrors"] = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["avatarFile"] = "Ảnh không hợp lệ. Chỉ chấp nhận JPG, JPEG, PNG, GIF, TIFF, SVG và tối đa 20MB."
                });
                return RedirectToAction(nameof(Index), new { editId = petId });
            }

            DeletePetImageIfLocal(pet.ImageUrl);
            pet.ImageUrl = newImageUrl;
        }

        pet.Name = name.Trim();
        pet.Species = species.Trim();
        pet.Breed = breed.Trim();
        pet.Age = FormatAge(dateOfBirth!.Value);
        pet.Weight = weight;
        pet.Pathology = string.IsNullOrWhiteSpace(pathology) ? "Khỏe mạnh" : pathology.Trim();

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã cập nhật hồ sơ bé [{pet.Name}] thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int petId)
    {
        var pet = await GetOwnedPetAsync(petId);
        if (pet == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy thú cưng hoặc bạn không có quyền xóa.";
            return RedirectToAction(nameof(Index));
        }

        var petName = pet.Name;
        DeletePetImageIfLocal(pet.ImageUrl);
        _context.Pets.Remove(pet);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã xóa hồ sơ bé [{petName}] khỏi hệ thống.";
        return RedirectToAction(nameof(Index));
    }

    //Hàm này có nhiệm vụ tổng hợp toàn bộ dữ liệu cần thiết để hiển thị trang hồ sơ thú cưng
    private async Task<PetProfilePageViewModel?> BuildPageViewModelAsync()
    {
        //Tìm trong các claims của người dùng hiện tại (thường được tạo ra khi đăng nhập) để lấy ID người dùng.
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);  // 
        if (userIdClaim == null)
        {
            return null;
        }

        var userId = int.Parse(userIdClaim.Value);
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Customer)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user?.Customer == null)
        {
            return null;
        }

        var pets = await _context.Pets  // lay danh sách thú cưng của khách đó 
            .Where(p => p.CustomerId == user.Customer.CustomerId)
            .OrderByDescending(p => p.PetId)  // ắp xếp giảm dần theo PetId (thú cưng mới thêm sẽ hiện lên đầu).
            .ToListAsync();

        return new PetProfilePageViewModel
        {
            User = user,
            Customer = user.Customer,
            Pets = pets,
            OpenCreateModal = TempData["OpenCreateModal"] != null
        };
    }
    // hàm chi chỉ tập trung vào việc lấy CustomerId từ phiên đăng nhập.
    private async Task<int?> GetCurrentCustomerIdAsync()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            return null;
        }

        var userId = int.Parse(userIdClaim.Value);
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
        return customer?.CustomerId;  // Lấy UserId từ token -> tìm Customer tương ứng trong DB -> trả về CustomerId.
    }


    //Đây là hàm bắt buộc phải gọi trước khi Sửa hoặc Xóa để chống lỗi Broken Object Level Authorization
    private async Task<Pet?> GetOwnedPetAsync(int petId)  
    {
        var customerId = await GetCurrentCustomerIdAsync();  // lay customerID hiện tại 
        if (customerId == null)
        {
            return null;
        }

        return await _context.Pets.FirstOrDefaultAsync(p => p.PetId == petId && p.CustomerId == customerId.Value);
    }
    // chuyển đổi dữ liệu 
    // Chuyển Entity Pet thành PetFormModel để đổ dữ liệu vào các thẻ input trong Form Edit.
    private static PetFormModel MapPetToForm(Pet pet)
    {
        return new PetFormModel
        {
            PetId = pet.PetId,
            Name = pet.Name,
            Species = pet.Species,
            Breed = pet.Breed ?? "",
            Age = pet.Age ?? "",
            Weight = pet.Weight,
            Pathology = pet.Pathology ?? "",
            CurrentImageUrl = pet.ImageUrl
        };
    }

    private static Dictionary<string, string> CollectPetFormErrors(
        string name,
        string species,
        string breed,
        DateTime? dateOfBirth,
        decimal weight,
        IFormFile? avatarFile)
    {
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!IsLettersOnly(name))
        {
            errors["name"] = "Tên thú cưng phải có ít nhất 1 ký tự, chỉ được chứa chữ cái.";
        }

        if (!IsLettersOnly(species))
        {
            errors["species"] = "Giống loài phải có ít nhất 1 ký tự, chỉ được chứa chữ cái.";
        }

        if (!IsLettersOnly(breed))
        {
            errors["breed"] = "Giống (breed) phải có ít nhất 1 ký tự, chỉ được chứa chữ cái.";
        }

        if (!ValidateDateOfBirth(dateOfBirth, out var dobError))
        {
            errors["dateOfBirth"] = dobError;
        }

        if (!ValidateWeight(weight, out var weightError))
        {
            errors["weight"] = weightError;
        }

        if (!ValidatePetImage(avatarFile, out var imageError))
        {
            errors["avatarFile"] = imageError;
        }

        return errors;
    }

    private static bool IsLettersOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim().All(c => char.IsLetter(c) || char.IsWhiteSpace(c));
    }

    private static bool ValidateDateOfBirth(DateTime? dateOfBirth, out string errorMessage)
    {
        errorMessage = "";

        if (!dateOfBirth.HasValue)
        {
            errorMessage = "Vui lòng chọn ngày sinh.";
            return false;
        }

        var birthDate = dateOfBirth.Value.Date;
        if (birthDate > DateTime.Today)
        {
            errorMessage = "Ngày sinh phải nhỏ hơn thời gian hiện tại.";
            return false;
        }

        if (birthDate.Year < 2000)
        {
            errorMessage = "Ngày sinh phải từ năm 2000 đến nay.";
            return false;
        }

        return true;
    }

    private static bool ValidateWeight(decimal weight, out string errorMessage)
    {
        errorMessage = "";

        var weightText = weight.ToString(CultureInfo.InvariantCulture);
        if (!WeightPattern.IsMatch(weightText) || weight <= 0)
        {
            errorMessage = "Cân nặng phải là số lớn hơn 0, không chứa chữ cái hoặc ký tự đặc biệt.";
            return false;
        }

        return true;
    }

    private static bool ValidatePetImage(IFormFile? file, out string errorMessage)
    {
        errorMessage = "";

        if (file == null || file.Length == 0)
        {
            return true;
        }

        if (file.Length > MaxImageBytes)
        {
            errorMessage = "Ảnh avatar không được vượt quá 20MB.";
            return false;
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(extension))
        {
            errorMessage = "Ảnh chỉ được phép định dạng JPG, JPEG, PNG, GIF, TIFF, SVG.";
            return false;
        }

        return true;
    }

    private async Task<string?> SavePetImageAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return null;
        }

        if (!ValidatePetImage(file, out _))
        {
            return null;
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension == ".tif")
        {
            extension = ".tiff";
        }

        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "pets"); 
        Directory.CreateDirectory(uploadsFolder);  // sẽ tạo thư mục nếu nó chưa tồn tại (chạy lần đầu).

        var fileName = $"{Guid.NewGuid():N}{extension}";  // Tạo tên file ngẫu nhiên bằng Guid để chống trùng lặp tên
        var filePath = Path.Combine(uploadsFolder, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return $"/uploads/pets/{fileName}";
    }

    // xóa ảnh cũ 
    private void DeletePetImageIfLocal(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || !imageUrl.StartsWith("/uploads/pets/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var filePath = Path.Combine(_env.WebRootPath, imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
    }

    private static string FormatAge(DateTime birthDate)
    {
        var today = DateTime.Today;
        var totalMonths = (today.Year - birthDate.Year) * 12 + today.Month - birthDate.Month;
        if (today.Day < birthDate.Day)
        {
            totalMonths--;
        }

        if (totalMonths < 12)
        {
            return $"{Math.Max(totalMonths, 1)} tháng";
        }

        var years = totalMonths / 12;
        var months = totalMonths % 12;
        return months > 0 ? $"{years} tuổi {months} tháng" : $"{years} tuổi";
    }

    [HttpGet]
    public async Task<IActionResult> GetMedicalRecords(int petId)
    {
        var customerId = await GetCurrentCustomerIdAsync();
        if (customerId == null)
        {
            return Json(new { success = false, message = "Bạn chưa đăng nhập." });
        }

        var pet = await _context.Pets.FirstOrDefaultAsync(p => p.PetId == petId && p.CustomerId == customerId.Value);
        if (pet == null)
        {
            return Json(new { success = false, message = "Không tìm thấy thú cưng hoặc bạn không có quyền truy cập." });
        }

        var records = await _context.MedicalRecords
            .Where(mr => mr.PetId == petId)
            .OrderByDescending(mr => mr.DateCreated)
            .Select(r => new {
                r.RecordId,
                r.PetId,
                DateCreated = r.DateCreated.ToString("dd/MM/yyyy HH:mm"),
                r.Weight,
                r.HealthStatus,
                Symptoms = r.Symptoms ?? "",
                Treatment = r.Treatment ?? "",
                VaccinationStatus = r.VaccinationStatus ?? "",
                ParasitePrevention = r.ParasitePrevention ?? "",
                PhysicalCheck = r.PhysicalCheck ?? "",
                ShellStatus = r.ShellStatus ?? "",
                RearingConditions = r.RearingConditions ?? "",
                AbnormalSymptoms = r.AbnormalSymptoms ?? "",
                IncisorCheck = r.IncisorCheck ?? "",
                FurSkinCheck = r.FurSkinCheck ?? "",
                DigestiveSigns = r.DigestiveSigns ?? ""
            })
            .ToListAsync();

        return Json(new { success = true, data = records });
    }

    [HttpGet]
    public async Task<IActionResult> MedicalHistory(int? petId)
    {
        var customerId = await GetCurrentCustomerIdAsync();
        if (customerId == null)
        {
            return RedirectToAction("Login", "CustomerAccount");
        }

        var userClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userClaim == null)
        {
            return RedirectToAction("Login", "CustomerAccount");
        }
        var userId = int.Parse(userClaim.Value);
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Customer)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user?.Customer == null)
        {
            return RedirectToAction("Login", "CustomerAccount");
        }

        var allPets = await _context.Pets
            .Where(p => p.CustomerId == customerId.Value)
            .OrderByDescending(p => p.PetId)
            .ToListAsync();

        if (allPets.Count == 0)
        {
            TempData["ErrorMessage"] = "Bạn chưa đăng ký hồ sơ thú cưng nào. Vui lòng thêm thú cưng trước.";
            return RedirectToAction(nameof(Index));
        }

        if (!petId.HasValue)
        {
            return RedirectToAction(nameof(MedicalHistory), new { petId = allPets.First().PetId });
        }

        var selectedPet = allPets.FirstOrDefault(p => p.PetId == petId.Value);
        if (selectedPet == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy thú cưng hoặc bạn không có quyền truy cập.";
            return RedirectToAction(nameof(Index));
        }

        var records = await _context.MedicalRecords
            .Where(mr => mr.PetId == selectedPet.PetId)
            .OrderByDescending(mr => mr.DateCreated)
            .ToListAsync();

        var viewModel = new PetMedicalHistoryViewModel
        {
            User = user,
            Customer = user.Customer,
            AllPets = allPets,
            SelectedPet = selectedPet,
            MedicalRecords = records
        };

        return View(viewModel);
    }


    private static string GetDefaultPetImage(string species)
    {
        var spec = species.ToLowerInvariant();
        if (spec.Contains("mèo") || spec.Contains("cat"))
        {
            return "https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?w=200&h=200&fit=crop";
        }
        else if (spec.Contains("rùa") || spec.Contains("turtle"))
        {
            return "https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?w=200&h=200&fit=crop";
        }
        else if (spec.Contains("chuột") || spec.Contains("hamster"))
        {
            return "https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?w=200&h=200&fit=crop";
        }
        return "https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?w=200&h=200&fit=crop";
    }
}




