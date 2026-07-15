namespace ManagePetStore.Models;

public sealed class HotelCheckInAssessment
{
    public int AssessmentId { get; set; }
    public int HotelBookingId { get; set; }
    public int MedicalRecordId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string? Note { get; set; }
    public int? AssessedByUserId { get; set; }
    public string AssessedByName { get; set; } = string.Empty;
    public DateTime AssessedAt { get; set; }

    public HotelBooking HotelBooking { get; set; } = null!;
    public MedicalRecord MedicalRecord { get; set; } = null!;
    public User? AssessedByUser { get; set; }
}
