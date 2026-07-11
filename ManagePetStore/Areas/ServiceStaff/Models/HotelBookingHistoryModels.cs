using ManagePetStore.Models;

namespace ManagePetStore.Areas.ServiceStaff.Models;

public class StaffHotelBookingHistoryPageViewModel
{
    public List<StaffHotelBookingHistoryRowViewModel> Bookings { get; set; } = [];
    public List<StaffHotelPetOptionViewModel> Pets { get; set; } = [];
    public string SearchTerm { get; set; } = string.Empty;
    public string StatusFilter { get; set; } = "all";
    public int? PetId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public class StaffHotelBookingHistoryRowViewModel
{
    public int HotelBookingId { get; set; }
    public string DisplayBookingId => $"HB{HotelBookingId:0000}";
    public int PetId { get; set; }
    public string PetName { get; set; } = string.Empty;
    public string PetSpecies { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CageId { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;
    public DateTime CheckInDate { get; set; }
    public DateTime? CheckOutDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusKey { get; set; } = string.Empty;
    public decimal FinalAmount { get; set; }
}

public class StaffHotelPetOptionViewModel
{
    public int PetId { get; set; }
    public string PetName { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public int BookingCount { get; set; }
}

public class StaffHotelBookingDetailPageViewModel
{
    public HotelBookingHistoryDetailViewModel Booking { get; set; } = new();
}
