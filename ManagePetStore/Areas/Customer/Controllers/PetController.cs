using System.Security.Claims;
using ManagePetStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Areas.Customer.Controllers;

[Area("Customer")]
[Authorize]
public class PetController : Controller
{
    private readonly PetStoreManagementContext _context;
    private readonly IWebHostEnvironment _env;
    private static readonly string[] AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];

    public PetController(PetStoreManagementContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? editId = null)
    {
        var page = await BuildPageViewModelAsync();
        if (page == null)
        {
            return RedirectToAction("Login", "Account", new { area = "Customer" });
        }

        if (editId.HasValue)
        {
            var pet = await GetOwnedPetAsync(editId.Value);
            if (pet == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thú cưng hoặc bạn không có quyền truy cập.";
                return RedirectToAction(nameof(Index));
            }

            page.EditPet = MapPetToForm(pet);
            page.OpenEditModal = true;
        }

        return View(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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
            return RedirectToAction("Login", "Account", new { area = "Customer" });
        }

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(species) ||
            string.IsNullOrWhiteSpace(breed) ||
            !dateOfBirth.HasValue ||
            weight <= 0)
        {
            TempData["ErrorMessage"] = "Vui lòng điền đầy đủ các trường bắt buộc (*).";
            TempData["OpenCreateModal"] = true;
            return RedirectToAction(nameof(Index));
        }

        if (dateOfBirth.Value.Date > DateTime.Today)
        {
            TempData["ErrorMessage"] = "Ngày sinh không hợp lệ.";
            TempData["OpenCreateModal"] = true;
            return RedirectToAction(nameof(Index));
        }

        var imageUrl = await SavePetImageAsync(avatarFile);
        if (avatarFile != null && avatarFile.Length > 0 && imageUrl == null)
        {
            TempData["ErrorMessage"] = "Ảnh không hợp lệ. Chỉ chấp nhận JPG, PNG, GIF, WEBP.";
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
            Age = FormatAge(dateOfBirth.Value),
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
    public async Task<IActionResult> Edit(
        int petId,
        string name,
        string species,
        string breed,
        string age,
        decimal weight,
        string? pathology,
        IFormFile? avatarFile)
    {
        var pet = await GetOwnedPetAsync(petId);
        if (pet == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy thú cưng hoặc bạn không có quyền chỉnh sửa.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(species) ||
            string.IsNullOrWhiteSpace(breed) ||
            string.IsNullOrWhiteSpace(age) ||
            weight <= 0)
        {
            TempData["ErrorMessage"] = "Vui lòng điền đầy đủ các trường bắt buộc (*).";
            return RedirectToAction(nameof(Index), new { editId = petId });
        }

        if (avatarFile != null && avatarFile.Length > 0)
        {
            var newImageUrl = await SavePetImageAsync(avatarFile);
            if (newImageUrl == null)
            {
                TempData["ErrorMessage"] = "Ảnh không hợp lệ. Chỉ chấp nhận JPG, PNG, GIF, WEBP.";
                return RedirectToAction(nameof(Index), new { editId = petId });
            }

            DeletePetImageIfLocal(pet.ImageUrl);
            pet.ImageUrl = newImageUrl;
        }

        pet.Name = name.Trim();
        pet.Species = species.Trim();
        pet.Breed = breed.Trim();
        pet.Age = age.Trim();
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

    private async Task<PetProfilePageViewModel?> BuildPageViewModelAsync()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
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

        var pets = await _context.Pets
            .Where(p => p.CustomerId == user.Customer.CustomerId)
            .OrderByDescending(p => p.PetId)
            .ToListAsync();

        return new PetProfilePageViewModel
        {
            User = user,
            Customer = user.Customer,
            Pets = pets,
            OpenCreateModal = TempData["OpenCreateModal"] != null
        };
    }

    private async Task<int?> GetCurrentCustomerIdAsync()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            return null;
        }

        var userId = int.Parse(userIdClaim.Value);
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
        return customer?.CustomerId;
    }

    private async Task<Pet?> GetOwnedPetAsync(int petId)
    {
        var customerId = await GetCurrentCustomerIdAsync();
        if (customerId == null)
        {
            return null;
        }

        return await _context.Pets.FirstOrDefaultAsync(p => p.PetId == petId && p.CustomerId == customerId.Value);
    }

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

    private async Task<string?> SavePetImageAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return null;
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(extension))
        {
            return null;
        }

        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "pets");
        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return $"/uploads/pets/{fileName}";
    }

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

    private static string GetDefaultPetImage(string species)
    {
        return species.Contains("mèo", StringComparison.OrdinalIgnoreCase) ||
               species.Contains("cat", StringComparison.OrdinalIgnoreCase)
            ? "https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?w=200&h=200&fit=crop"
            : "https://images.unsplash.com/photo-1587300003388-59208cc962cb?w=200&h=200&fit=crop";
    }
}
