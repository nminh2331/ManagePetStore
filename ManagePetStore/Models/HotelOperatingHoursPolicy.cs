namespace ManagePetStore.Models;

public static class HotelOperatingHoursPolicy
{
    public static readonly TimeSpan PetHandoverOpenTime = new(7, 0, 0);
    public static readonly TimeSpan PetHandoverLastTime = new(21, 30, 0);

    public const string ExpectedCheckoutError =
        "Thời gian trả dự kiến phải trong khung giờ bàn giao thú cưng từ 07:00 đến 21:30.";

    // [nam][BR] Bàn giao pet kết thúc trước giờ đóng cửa để Staff còn thời gian kiểm tra và thanh toán.
    public static bool IsExpectedCheckoutWithinHandoverHours(DateTime expectedCheckout)
    {
        TimeSpan time = expectedCheckout.TimeOfDay;
        return time >= PetHandoverOpenTime && time <= PetHandoverLastTime;
    }
}
