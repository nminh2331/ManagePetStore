using ManagePetStore.Exceptions;
using ManagePetStore.Models;
using ManagePetStore.Repositories;

namespace ManagePetStore.Services;

public class RoomService : IRoomService
{
    private readonly IRoomRepository _repository;

    public static readonly string[] RoomTypes =
        ["Standard", "VIP", "Suite", "ICU"];

    private static readonly string[] ValidStatuses =
    [
        RoomStatus.Available,
        RoomStatus.Occupied,
        RoomStatus.Cleaning,
        RoomStatus.Maintenance
    ];

    public RoomService(IRoomRepository repository)
    {
        _repository = repository;
    }

    // --- L?y danh s�ch (c� filter) --------------------------------------------

    public RoomListViewModel GetAll(string? statusFilter = null)
    {
        var all = _repository.GetAll().ToList();

        var filtered = string.IsNullOrWhiteSpace(statusFilter)
            ? all
            : all.Where(r => r.Status == statusFilter).ToList();

        return new RoomListViewModel
        {
            Rooms            = filtered.Select(MapToViewModel),
            StatusFilter     = statusFilter,
            TotalCount       = all.Count,
            AvailableCount   = all.Count(r => r.Status == RoomStatus.Available),
            OccupiedCount    = all.Count(r => r.Status == RoomStatus.Occupied),
            CleaningCount    = all.Count(r => r.Status == RoomStatus.Cleaning),
            MaintenanceCount = all.Count(r => r.Status == RoomStatus.Maintenance)
        };
    }

    // --- L?y chi ti?t 1 chu?ng ------------------------------------------------

    public RoomViewModel? GetById(int id)
    {
        var room = _repository.GetById(id);
        return room is null ? null : MapToViewModel(room);
    }

    // --- T?o m?i --------------------------------------------------------------

    public void Create(RoomViewModel model)
    {
        ValidateModel(model, excludeId: null);

        var room = new Room
        {
            RoomCode   = model.RoomCode.Trim(),
            RoomType   = model.RoomType,
            DailyRate  = model.DailyRate,
            Status     = model.Status ?? RoomStatus.Available,
            Dimensions = string.IsNullOrWhiteSpace(model.Dimensions) ? null : model.Dimensions.Trim()
        };

        _repository.Create(room);
    }

    // --- C?p nh?t -------------------------------------------------------------

    public void Update(RoomViewModel model)
    {
        var existing = _repository.GetById(model.RoomId)
            ?? throw new ServiceException("Kh�ng t�m th?y chu?ng c?n c?p nh?t.");

        ValidateModel(model, excludeId: model.RoomId);

        existing.RoomCode   = model.RoomCode.Trim();
        existing.RoomType   = model.RoomType;
        existing.DailyRate  = model.DailyRate;
        existing.Status     = model.Status ?? RoomStatus.Available;
        existing.Dimensions = string.IsNullOrWhiteSpace(model.Dimensions) ? null : model.Dimensions.Trim();

        _repository.Update(existing);
    }

    // --- X�a ------------------------------------------------------------------

    public void Delete(int id)
    {
        _ = _repository.GetById(id)
            ?? throw new ServiceException("Kh�ng t�m th?y chu?ng c?n x�a.");

        _repository.Delete(id);
    }

    // --- �?i tr?ng th�i nhanh -------------------------------------------------

    public void UpdateStatus(int id, string status)
    {
        if (!ValidStatuses.Contains(status))
            throw new ServiceException($"Tr?ng th�i '{status}' kh�ng h?p l?.");

        var room = _repository.GetById(id)
            ?? throw new ServiceException("Kh�ng t�m th?y chu?ng.");

        room.Status = status;
        _repository.Update(room);
    }

    // --- Private helpers ------------------------------------------------------

    private void ValidateModel(RoomViewModel model, int? excludeId)
    {
        if (_repository.ExistsByRoomCode(model.RoomCode.Trim(), excludeId))
            throw new ServiceException("M� chu?ng d� t?n t?i.");

        if (!RoomTypes.Contains(model.RoomType))
            throw new ServiceException($"Lo?i chu?ng '{model.RoomType}' kh�ng h?p l?.");

        if (!string.IsNullOrEmpty(model.Status) && !ValidStatuses.Contains(model.Status))
            throw new ServiceException($"Tr?ng th�i '{model.Status}' kh�ng h?p l?.");
    }

    private static RoomViewModel MapToViewModel(Room room) => new()
    {
        RoomId     = room.RoomId,
        RoomCode   = room.RoomCode,
        RoomType   = room.RoomType,
        DailyRate  = room.DailyRate,
        Status     = room.Status ?? RoomStatus.Available,
        Dimensions = room.Dimensions
    };
}
