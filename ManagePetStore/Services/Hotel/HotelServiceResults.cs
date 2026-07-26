namespace ManagePetStore.Services.Hotel;

public sealed record HotelCommandResult(bool Success, string Message)
{
    // [nam] Tạo kết quả thành công cho một lệnh nghiệp vụ Hotel.
    public static HotelCommandResult Ok(string message) => new(true, message);

    // [nam] Tạo kết quả thất bại kèm thông báo nghiệp vụ Hotel.
    public static HotelCommandResult Fail(string message) => new(false, message);
}

public sealed record HotelAvailableCage(string CageId, string Status);

public sealed record HotelAvailabilityResult(
    bool Success,
    string Message,
    IReadOnlyList<HotelAvailableCage> Cages)
{
    // [nam] Tạo kết quả truy vấn chuồng trống thành công.
    public static HotelAvailabilityResult Ok(IReadOnlyList<HotelAvailableCage> cages) =>
        new(true, string.Empty, cages);

    // [nam] Tạo kết quả truy vấn chuồng trống thất bại.
    public static HotelAvailabilityResult Fail(string message) =>
        new(false, message, Array.Empty<HotelAvailableCage>());
}
