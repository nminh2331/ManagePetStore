using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ManagePetStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ChatApiController : ControllerBase
{
    private readonly PetStoreManagementContext _context;
    private readonly ILogger<ChatApiController> _logger;

    public ChatApiController(PetStoreManagementContext context, ILogger<ChatApiController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // =========================================================================
    // GET CURRENT USER - Lấy thông tin user hiện tại đang login
    // =========================================================================
    [HttpGet("me")]
    public IActionResult GetCurrentUser()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Ok(new { isAuthenticated = false });
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        var fullNameClaim = User.FindFirst("FullName")?.Value ?? User.Identity.Name ?? "User";
        var roleIdClaim = User.FindFirst("RoleId")?.Value ?? "5"; // Mặc định là 5 (Customer)

        return Ok(new
        {
            isAuthenticated = true,
            userId = userIdClaim != null ? int.Parse(userIdClaim.Value) : 0,
            fullName = fullNameClaim,
            roleId = int.Parse(roleIdClaim)
        });
    }

    // =========================================================================
    // GET MESSAGES - Lấy 50 tin nhắn gần nhất của CustomerId
    // Sắp xếp theo thời gian tăng dần để render từ trên xuống dưới
    // =========================================================================
    [HttpGet("messages/{customerId}")]
    public async Task<IActionResult> GetMessages(int customerId)
    {
        try
        {
            // Kiểm tra phân quyền: Người dùng phải đăng nhập
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized("Bạn cần đăng nhập để xem tin nhắn.");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            var roleIdClaim = User.FindFirst("RoleId")?.Value ?? "5";
            int currentUserId = int.Parse(userIdClaim!.Value);
            int roleId = int.Parse(roleIdClaim);

            // Nếu là Customer (RoleId = 5), chỉ được xem tin nhắn của chính mình
            if (roleId == 5 && currentUserId != customerId)
            {
                return Forbid("Bạn không có quyền xem tin nhắn của người khác.");
            }

            // Lấy 50 tin nhắn gần nhất của customerId này
            var messages = await _context.ChatMessages
                .Include(m => m.Sender)
                .Where(m => m.Session.CustomerId == customerId)
                .OrderByDescending(m => m.SentAt)
                .Take(50)
                .ToListAsync();

            // Đảo ngược lại để hiển thị từ cũ đến mới (tăng dần thời gian)
            var result = messages.OrderBy(m => m.SentAt).Select(m => new
            {
                id = m.Id,
                sessionId = m.SessionId,
                senderId = m.SenderId,
                senderName = m.Sender.FullName,
                messageText = m.MessageText,
                sentAt = m.SentAt.ToString("yyyy-MM-ddTHH:mm:ss")
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lấy tin nhắn của CustomerId={CustomerId}.", customerId);
            return StatusCode(500, "Lỗi hệ thống khi tải tin nhắn.");
        }
    }

    // =========================================================================
    // GET SESSIONS - Lấy danh sách phiên chat (Dành cho Manager/Admin)
    // Sắp xếp OrderByDescending(LastMessageAt)
    // =========================================================================
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        try
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized();
            }

            var roleIdClaim = User.FindFirst("RoleId")?.Value ?? "5";
            int roleId = int.Parse(roleIdClaim);

            // Chỉ Manager (6) hoặc Admin mới được xem
            if (roleId != 6 && !User.IsInRole("admin"))
            {
                return Forbid("Bạn không có quyền xem danh sách phiên chat.");
            }

            // Live Sessions (Waiting & Active)
            var liveSessions = await _context.ChatSessions
                .Include(s => s.Customer)
                .Include(s => s.Manager)
                .Where(s => s.Status == "Waiting" || s.Status == "Active")
                .OrderByDescending(s => s.LastMessageAt)
                .Select(s => new
                {
                    id = s.Id,
                    customerId = s.CustomerId,
                    customerName = s.Customer.FullName,
                    managerId = s.ManagerId,
                    managerName = s.Manager != null ? s.Manager.FullName : null,
                    status = s.Status,
                    createdAt = s.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    lastMessageAt = s.LastMessageAt.ToString("yyyy-MM-ddTHH:mm:ss")
                })
                .ToListAsync();

            // History Sessions (Closed)
            var historySessions = await _context.ChatSessions
                .Include(s => s.Customer)
                .Include(s => s.Manager)
                .Where(s => s.Status == "Closed")
                .OrderByDescending(s => s.LastMessageAt)
                .Select(s => new
                {
                    id = s.Id,
                    customerId = s.CustomerId,
                    customerName = s.Customer.FullName,
                    managerId = s.ManagerId,
                    managerName = s.Manager != null ? s.Manager.FullName : null,
                    status = s.Status,
                    createdAt = s.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    lastMessageAt = s.LastMessageAt.ToString("yyyy-MM-ddTHH:mm:ss")
                })
                .ToListAsync();

            return Ok(new
            {
                live = liveSessions,
                history = historySessions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lấy danh sách phiên chat của Manager.");
            return StatusCode(500, "Lỗi hệ thống khi tải phiên chat.");
        }
    }
}
