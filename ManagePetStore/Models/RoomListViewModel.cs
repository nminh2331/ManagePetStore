namespace ManagePetStore.Models;

public class RoomListViewModel
{
    public IEnumerable<RoomViewModel> Rooms { get; set; } = Enumerable.Empty<RoomViewModel>();
    public string? StatusFilter { get; set; }
    public int TotalCount { get; set; }
    public int AvailableCount { get; set; }
    public int OccupiedCount { get; set; }
    public int CleaningCount { get; set; }
    public int MaintenanceCount { get; set; }
}
