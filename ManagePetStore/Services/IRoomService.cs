using ManagePetStore.Models;

namespace ManagePetStore.Services;

public interface IRoomService
{
    RoomListViewModel GetAll(string? statusFilter = null);
    RoomViewModel? GetById(int id);
    void Create(RoomViewModel model);
    void Update(RoomViewModel model);
    void Delete(int id);
    void UpdateStatus(int id, string status);
}
