namespace ManagePetStore.Services;

public interface IHotelEmailService
{
    Task SendBookingCreatedAsync(string? email, string customerName, int bookingId, string petName,
        string cageId, string roomTypeName, DateTime checkIn, DateTime checkOut, decimal totalAmount);

    Task SendCheckInAsync(string? email, string customerName, int bookingId, string petName,
        string cageId, DateTime checkIn, DateTime? expectedCheckOut);

    Task SendCheckOutAsync(string? email, string customerName, int bookingId, string petName,
        string cageId, DateTime checkOut, decimal totalAmount);

    Task SendCareLogAsync(string? email, string customerName, int bookingId, string petName,
        string title, string message, DateTime occurredAt);

    Task SendCageChangeDecisionAsync(string? email, string customerName, int bookingId, string petName,
        string sourceCageId, string targetCageId, bool approved, decimal priceDifference, string? note);
}
