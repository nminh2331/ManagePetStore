namespace ManagePetStore.Models;

public static class HotelOperatingHoursPolicy
{
    public static readonly TimeSpan PetHandoverOpenTime = new(7, 0, 0);
    public static readonly TimeSpan PetHandoverLastTime = new(21, 30, 0);

    public const string ExpectedCheckInError =
        "Thời gian nhận phòng phải trong khung giờ tiếp nhận thú cưng từ 07:00 đến 21:30.";

    public const string ExpectedCheckoutError =
        "Thời gian trả dự kiến phải trong khung giờ bàn giao thú cưng từ 07:00 đến 21:30.";

    // [nam][BR] Cửa hàng chỉ tiếp nhận pet trong khung giờ nhân viên có thể bàn giao chuồng.
    public static bool IsExpectedCheckInWithinHandoverHours(DateTime expectedCheckIn)
    {
        return IsWithinHandoverHours(expectedCheckIn);
    }

    // [nam][BR] Bàn giao pet kết thúc trước giờ đóng cửa để Staff còn thời gian kiểm tra và thanh toán.
    public static bool IsExpectedCheckoutWithinHandoverHours(DateTime expectedCheckout)
    {
        return IsWithinHandoverHours(expectedCheckout);
    }

    private static bool IsWithinHandoverHours(DateTime dateTime)
    {
        TimeSpan time = dateTime.TimeOfDay;
        return time >= PetHandoverOpenTime && time <= PetHandoverLastTime;
    }
}
