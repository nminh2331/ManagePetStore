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

    // ─── Lấy danh sách (có filter) ────────────────────────────────────────────

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

    // ─── Lấy chi tiết 1 chuồng ────────────────────────────────────────────────

    public RoomViewModel? GetById(int id)
    {
        var room = _repository.GetById(id);
        return room is null ? null : MapToViewModel(room);
    }

    // ─── Tạo mới ──────────────────────────────────────────────────────────────

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

    // ─── Cập nhật ─────────────────────────────────────────────────────────────

    public void Update(RoomViewModel model)
    {
        var existing = _repository.GetById(model.RoomId)
            ?? throw new ServiceException("Không tìm thấy chuồng cần cập nhật.");

        ValidateModel(model, excludeId: model.RoomId);

        existing.RoomCode   = model.RoomCode.Trim();
        existing.RoomType   = model.RoomType;
        existing.DailyRate  = model.DailyRate;
        existing.Status     = model.Status ?? RoomStatus.Available;
        existing.Dimensions = string.IsNullOrWhiteSpace(model.Dimensions) ? null : model.Dimensions.Trim();

        _repository.Update(existing);
    }

    // ─── Xóa ──────────────────────────────────────────────────────────────────

    public void Delete(int id)
    {
        _ = _repository.GetById(id)
            ?? throw new ServiceException("Không tìm thấy chuồng cần xóa.");

        _repository.Delete(id);
    }

    // ─── Đổi trạng thái nhanh ─────────────────────────────────────────────────

    public void UpdateStatus(int id, string status)
    {
        if (!ValidStatuses.Contains(status))
            throw new ServiceException($"Trạng thái '{status}' không hợp lệ.");

        var room = _repository.GetById(id)
            ?? throw new ServiceException("Không tìm thấy chuồng.");

        room.Status = status;
        _repository.Update(room);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private void ValidateModel(RoomViewModel model, int? excludeId)
    {
        if (_repository.ExistsByRoomCode(model.RoomCode.Trim(), excludeId))
            throw new ServiceException("Mã chuồng đã tồn tại.");

        if (!RoomTypes.Contains(model.RoomType))
            throw new ServiceException($"Loại chuồng '{model.RoomType}' không hợp lệ.");

        if (!string.IsNullOrEmpty(model.Status) && !ValidStatuses.Contains(model.Status))
            throw new ServiceException($"Trạng thái '{model.Status}' không hợp lệ.");
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
