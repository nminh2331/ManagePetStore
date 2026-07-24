using Microsoft.AspNetCore.SignalR;

namespace ManagePetStore.Hubs
{
    public class ReviewHub : Hub
    {
        public async Task JoinServiceGroup(string sku)
        {
            if (!string.IsNullOrWhiteSpace(sku))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, sku.Trim());
            }
        }

        public async Task JoinStaffGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "StaffGroup");
        }
    }
}
