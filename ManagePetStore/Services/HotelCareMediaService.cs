using Microsoft.AspNetCore.Http;

namespace ManagePetStore.Services;

public class HotelCareMediaService : IHotelCareMediaService
{
    private const long MaxImageBytes = 10 * 1024 * 1024;
    private const long MaxVideoBytes = 50 * 1024 * 1024;
    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly HashSet<string> VideoExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".webm", ".mov" };

    private readonly IWebHostEnvironment _environment;

    // [nam] Khởi tạo dịch vụ lưu media nhật ký chăm sóc trong web root.
    public HotelCareMediaService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    // [nam] Kiểm tra loại, dung lượng, chữ ký tệp và lưu media theo từng booking.
    public async Task<HotelCareMediaResult?> SaveAsync(int hotelBookingId, IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return null;
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var isImage = ImageExtensions.Contains(extension);
        var isVideo = VideoExtensions.Contains(extension);
        if (!isImage && !isVideo)
        {
            throw new InvalidOperationException("Chỉ hỗ trợ JPG, PNG, WEBP, MP4, WEBM hoặc MOV.");
        }

        var sizeLimit = isImage ? MaxImageBytes : MaxVideoBytes;
        if (file.Length > sizeLimit)
        {
            throw new InvalidOperationException(isImage
                ? "Ảnh không được vượt quá 10 MB."
                : "Video không được vượt quá 50 MB.");
        }

        await using var signatureStream = file.OpenReadStream();
        if (!await HasExpectedSignatureAsync(signatureStream, extension))
        {
            throw new InvalidOperationException("Nội dung tệp không khớp với định dạng đã chọn.");
        }

        var relativeDirectory = Path.Combine("uploads", "hotel-care", hotelBookingId.ToString());
        var physicalDirectory = Path.Combine(_environment.WebRootPath, relativeDirectory);
        Directory.CreateDirectory(physicalDirectory);

        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(physicalDirectory, storedFileName);
        await using var source = file.OpenReadStream();
        await using var destination = File.Create(physicalPath);
        await source.CopyToAsync(destination);

        var publicUrl = "/" + Path.Combine(relativeDirectory, storedFileName).Replace('\\', '/');
        return new HotelCareMediaResult(publicUrl, isImage ? "Image" : "Video");
    }

    // [nam] Xoá tệp media chỉ khi đường dẫn nằm trong thư mục hotel-care được cho phép.
    public Task DeleteAsync(string? publicUrl)
    {
        if (string.IsNullOrWhiteSpace(publicUrl) || !publicUrl.StartsWith("/uploads/hotel-care/", StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var relativePath = publicUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var physicalPath = Path.GetFullPath(Path.Combine(_environment.WebRootPath, relativePath));
        var uploadRoot = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads", "hotel-care"));
        if (physicalPath.StartsWith(uploadRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        return Task.CompletedTask;
    }

    // [nam] Đối chiếu magic bytes để phát hiện tệp giả mạo phần mở rộng.
    private static async Task<bool> HasExpectedSignatureAsync(Stream stream, string extension)
    {
        var header = new byte[12];
        var bytesRead = await stream.ReadAsync(header.AsMemory(0, header.Length));
        if (bytesRead < 4)
        {
            return false;
        }

        return extension switch
        {
            ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => header.Take(8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".webp" => bytesRead >= 12 && System.Text.Encoding.ASCII.GetString(header, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(header, 8, 4) == "WEBP",
            ".mp4" or ".mov" => bytesRead >= 8 && System.Text.Encoding.ASCII.GetString(header, 4, 4) == "ftyp",
            ".webm" => header.Take(4).SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }),
            _ => false
        };
    }
}
