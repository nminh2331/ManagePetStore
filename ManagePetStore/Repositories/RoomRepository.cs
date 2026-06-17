using ManagePetStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Repositories;

public class RoomRepository : IRoomRepository
{
    private readonly PetStoreManagementContext _context;

    public RoomRepository(PetStoreManagementContext context)
    {
        _context = context;
    }

    public IEnumerable<Room> GetAll()
    {
        return _context.Set<Room>()
            .OrderBy(r => r.RoomCode)
            .ToList();
    }

    public Room? GetById(int id)
    {
        return _context.Set<Room>().FirstOrDefault(r => r.RoomId == id);
    }

    public IEnumerable<Room> GetByStatus(string status)
    {
        return _context.Set<Room>()
            .Where(r => r.Status == status)
            .OrderBy(r => r.RoomCode)
            .ToList();
    }

    public void Create(Room room)
    {
        _context.Set<Room>().Add(room);
        _context.SaveChanges();
    }

    public void Update(Room room)
    {
        _context.Set<Room>().Update(room);
        _context.SaveChanges();
    }

    public void Delete(int id)
    {
        var room = _context.Set<Room>().Find(id);
        if (room is not null)
        {
            _context.Set<Room>().Remove(room);
            _context.SaveChanges();
        }
    }

    public bool ExistsByRoomCode(string roomCode, int? excludeId = null)
    {
        return _context.Set<Room>().Any(r =>
            r.RoomCode == roomCode &&
            (excludeId == null || r.RoomId != excludeId));
    }
}
