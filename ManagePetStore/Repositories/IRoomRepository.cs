using ManagePetStore.Model;

namespace ManagePetStore.Repositories;

public interface IRoomRepository
{
    IEnumerable<Room> GetAll();
    Room? GetById(int id);
    IEnumerable<Room> GetByStatus(string status);
    void Create(Room room);
    void Update(Room room);
    void Delete(int id);
    bool ExistsByRoomCode(string roomCode, int? excludeId = null);
}
