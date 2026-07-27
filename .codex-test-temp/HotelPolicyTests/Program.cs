using System.ComponentModel.DataAnnotations;
using ManagePetStore.Areas.Customer.Models;
using ManagePetStore.Areas.ServiceStaff.Models;
using ManagePetStore.Models;

var failures = new List<string>();
var executed = 0;

void Check(string name, bool condition)
{
    executed++;
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")} | {name}");
    if (!condition) failures.Add(name);
}

List<ValidationResult> Validate(object model)
{
    var results = new List<ValidationResult>();
    Validator.TryValidateObject(model, new ValidationContext(model), results, true);
    return results;
}

bool HasCheckoutHoursError(IEnumerable<ValidationResult> results) =>
    results.Any(result => result.ErrorMessage == HotelOperatingHoursPolicy.ExpectedCheckoutError);

DateTime testDay = DateTime.Today.AddDays(2);

Check("06:59 bị chặn", !HotelOperatingHoursPolicy.IsExpectedCheckoutWithinHandoverHours(testDay.AddHours(6).AddMinutes(59)));
Check("07:00 được chấp nhận", HotelOperatingHoursPolicy.IsExpectedCheckoutWithinHandoverHours(testDay.AddHours(7)));
Check("21:30 được chấp nhận", HotelOperatingHoursPolicy.IsExpectedCheckoutWithinHandoverHours(testDay.AddHours(21).AddMinutes(30)));
Check("21:31 bị chặn", !HotelOperatingHoursPolicy.IsExpectedCheckoutWithinHandoverHours(testDay.AddHours(21).AddMinutes(31)));

HotelBookingRequest CustomerRequest(DateTime checkout) => new()
{
    PetId = 1,
    RoomTypeId = 1,
    CageId = "A1",
    CheckInDate = testDay.AddHours(14),
    CheckOutDate = checkout,
    FoodProductSku = "CAGE-FOOD-DEFAULT"
};

Check("Customer checkout 21:30 hợp lệ", Validate(CustomerRequest(testDay.AddDays(1).AddHours(21).AddMinutes(30))).Count == 0);
Check("Customer checkout 21:31 nhận đúng lỗi giờ", HasCheckoutHoursError(Validate(CustomerRequest(testDay.AddDays(1).AddHours(21).AddMinutes(31)))));

HotelCheckInRequest StaffRequest(DateTime checkout) => new()
{
    ReceptionSource = HotelCheckInRequest.ExistingMedicalRecordSource,
    CustomerPhone = "0977445566",
    MedicalRecordId = 1,
    HealthStatus = HotelCheckInRequest.FitStatus,
    HealthCheckConfirmed = true,
    RoomTypeId = 1,
    CageId = "A1",
    CheckOutDate = checkout,
    FoodProductSku = "CAGE-FOOD-DEFAULT"
};

Check("Staff checkout 07:00 hợp lệ", Validate(StaffRequest(testDay.AddHours(7))).Count == 0);
Check("Staff checkout 06:59 nhận đúng lỗi giờ", HasCheckoutHoursError(Validate(StaffRequest(testDay.AddHours(6).AddMinutes(59)))));
Check("Staff request không còn CheckInDate", typeof(HotelCheckInRequest).GetProperty("CheckInDate") == null);
Check("Staff checkout quá 365 ngày bị chặn", Validate(StaffRequest(DateTime.Now.AddDays(366).Date.AddHours(14)))
    .Any(result => result.ErrorMessage?.Contains("365 ngày", StringComparison.Ordinal) == true));

Console.WriteLine($"TOTAL: {executed - failures.Count}/{executed} passed");
return failures.Count == 0 ? 0 : 1;
