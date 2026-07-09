using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ManagePetStore.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Hubs;

/// <summary>
/// Hub SignalR xử lý real-time chat giữa Customer và Manager
/// </summary>
public class ChatHub : Hub
{
    private readonly PetStoreManagementContext _context;

    public ChatHub(PetStoreManagementContext context)
    {
        _context = context;
    }

    // =========================================================================
    // JOIN GROUP - Cho phép Client (Customer hoặc Manager) tham gia vào group của CustomerId
    // =========================================================================
    public async Task JoinGroup(int customerId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"customer_{customerId}");
    }

    // =========================================================================
    // SEND MESSAGE - Gửi tin nhắn real-time
    // =========================================================================
    public async Task SendMessage(int customerId, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        try
        {
            // Xác định SenderId từ context User login
            var senderIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
            if (senderIdClaim == null)
            {
                throw new HubException("Bạn chưa đăng nhập.");
            }
            int senderId = int.Parse(senderIdClaim.Value);

            // Tìm session đang chờ (Waiting) hoặc đang hoạt động (Active) của Customer
            var session = await _context.ChatSessions
                .Where(s => s.CustomerId == customerId && (s.Status == "Waiting" || s.Status == "Active"))
                .OrderByDescending(s => s.LastMessageAt)
                .FirstOrDefaultAsync();

            bool isNewSession = false;
            // Tạo session mới nếu chưa có hoặc tin nhắn cuối đã quá 24h
            if (session == null || (DateTime.Now - session.LastMessageAt).TotalHours > 24)
            {
                // Nếu có session cũ nhưng đã quá 24h mà chưa Closed, tự động Close nó
                if (session != null)
                {
                    session.Status = "Closed";
                }

                session = new ChatSession
                {
                    CustomerId = customerId,
                    ManagerId = null,
                    Status = "Waiting",
                    CreatedAt = DateTime.Now,
                    LastMessageAt = DateTime.Now
                };
                _context.ChatSessions.Add(session);
                isNewSession = true;
            }
            else
            {
                session.LastMessageAt = DateTime.Now;
            }

            // Lưu ChatMessage vào DB
            var chatMsg = new ChatMessage
            {
                ChatSession = session,
                SenderId = senderId,
                MessageText = message,
                SentAt = DateTime.Now
            };
            _context.ChatMessages.Add(chatMsg);
            await _context.SaveChangesAsync();

            // Lấy thông tin người gửi
            var sender = await _context.Users.FindAsync(senderId);
            var senderName = sender?.FullName ?? "User";

            // Bắn tin nhắn real-time tới tất cả client trong group
            await Clients.Group($"customer_{customerId}").SendAsync("ReceiveMessage", new
            {
                id = chatMsg.Id,
                sessionId = session.Id,
                customerId = customerId,
                senderId = senderId,
                senderName = senderName,
                messageText = message,
                sentAt = chatMsg.SentAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                isNewSession = isNewSession
            });

            // Nếu là phiên mới, bắn thông báo cho hệ thống Manager để cập nhật Inbox List real-time
            if (isNewSession)
            {
                await Clients.All.SendAsync("NewSessionCreated", new
                {
                    id = session.Id,
                    customerId = customerId,
                    customerName = _context.Users.Find(customerId)?.FullName ?? "Khách hàng",
                    status = session.Status,
                    createdAt = session.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    lastMessageAt = session.LastMessageAt.ToString("yyyy-MM-ddTHH:mm:ss")
                });
            }
        }
        catch (Exception ex)
        {
            throw new HubException("Có lỗi xảy ra khi gửi tin nhắn: " + ex.Message);
        }
    }

    // =========================================================================
    // JOIN SESSION - Manager tiếp nhận phiên chat
    // =========================================================================
    public async Task JoinSession(int sessionId, int managerId)
    {
        try
        {
            var session = await _context.ChatSessions
                .Include(s => s.Customer)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null)
            {
                throw new HubException("Không tìm thấy phiên chat.");
            }

            session.Status = "Active";
            session.ManagerId = managerId;
            session.LastMessageAt = DateTime.Now;

            await _context.SaveChangesAsync();

            var manager = await _context.Users.FindAsync(managerId);
            var managerName = manager?.FullName ?? "Manager";

            // Bắn thông báo cập nhật trạng thái session tới group
            await Clients.Group($"customer_{session.CustomerId}").SendAsync("SessionStatusChanged", new
            {
                sessionId = sessionId,
                customerId = session.CustomerId,
                status = "Active",
                managerId = managerId,
                managerName = managerName,
                message = $"{managerName} đã tiếp nhận phiên chat."
            });

            // Báo cho toàn bộ hệ thống biết session này đã được tiếp nhận
            await Clients.All.SendAsync("SessionAssigned", new
            {
                sessionId = sessionId,
                managerId = managerId,
                managerName = managerName,
                status = "Active"
            });
        }
        catch (Exception ex)
        {
            throw new HubException("Lỗi tiếp nhận phiên: " + ex.Message);
        }
    }

    // =========================================================================
    // TAKE OVER SESSION - Manager khác cướp quyền/tiếp quản phiên chat
    // =========================================================================
    public async Task TakeOverSession(int sessionId, int newManagerId)
    {
        try
        {
            var session = await _context.ChatSessions.FindAsync(sessionId);
            if (session == null)
            {
                throw new HubException("Không tìm thấy phiên chat.");
            }

            session.ManagerId = newManagerId;
            session.LastMessageAt = DateTime.Now;
            await _context.SaveChangesAsync();

            var manager = await _context.Users.FindAsync(newManagerId);
            var managerName = manager?.FullName ?? "Manager";

            // Bắn thông báo cập nhật
            await Clients.Group($"customer_{session.CustomerId}").SendAsync("SessionStatusChanged", new
            {
                sessionId = sessionId,
                customerId = session.CustomerId,
                status = session.Status,
                managerId = newManagerId,
                managerName = managerName,
                message = $"{managerName} đã tiếp quản phiên chat này."
            });

            await Clients.All.SendAsync("SessionAssigned", new
            {
                sessionId = sessionId,
                managerId = newManagerId,
                managerName = managerName,
                status = session.Status
            });
        }
        catch (Exception ex)
        {
            throw new HubException("Lỗi tiếp quản phiên: " + ex.Message);
        }
    }

    // =========================================================================
    // CLOSE SESSION - Đóng phiên chat
    // =========================================================================
    public async Task CloseSession(int sessionId)
    {
        try
        {
            var session = await _context.ChatSessions.FindAsync(sessionId);
            if (session == null)
            {
                throw new HubException("Không tìm thấy phiên chat.");
            }

            session.Status = "Closed";
            session.LastMessageAt = DateTime.Now;
            await _context.SaveChangesAsync();

            // Gửi thông báo kết thúc tới group
            await Clients.Group($"customer_{session.CustomerId}").SendAsync("SessionStatusChanged", new
            {
                sessionId = sessionId,
                customerId = session.CustomerId,
                status = "Closed",
                message = "Phiên hỗ trợ đã được đóng."
            });

            await Clients.All.SendAsync("SessionClosed", new
            {
                sessionId = sessionId,
                status = "Closed"
            });
        }
        catch (Exception ex)
        {
            throw new HubException("Lỗi đóng phiên: " + ex.Message);
        }
    }
}
