using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ManagePetStore.Models;
using ManagePetStore.Areas.Customer.Models;

namespace ManagePetStore.Services.Customer;

public static class CustomerRewardHelper
{
    public static async Task RecalculateCustomerPointsAndTierAsync(int customerId, PetStoreManagementContext context)
    {
        var customer = await context.Customers
            .Include(c => c.Orders)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);
            
        if (customer != null)
        {
            // 1. Chỉ tính điểm cho đơn hàng Đã hoàn thành (10 điểm/đơn)
            var completedOrdersCount = customer.Orders.Count(o => 
                OrderStatusHelper.ResolveStatusKey(o.Status) == "completed"
            );
            
            var pointsEarned = completedOrdersCount * 10;
            
            // 2. Điểm đã dùng (trừ điểm khi đặt đơn hàng chưa bị hủy/từ chối)
            var pointsRedeemed = customer.Orders
                .Where(o => {
                    var statusKey = OrderStatusHelper.ResolveStatusKey(o.Status);
                    return statusKey != "cancelled" && statusKey != "rejected";
                })
                .Sum(o => o.PointsRedeemed);
                
            customer.LoyaltyPoints = Math.Max(0, pointsEarned - pointsRedeemed);
            
            // 3. Phân hạng thành viên theo bậc điểm
            if (customer.LoyaltyPoints >= 400)
            {
                customer.MembershipTier = "VIP";
            }
            else if (customer.LoyaltyPoints >= 300)
            {
                customer.MembershipTier = "Vàng";
            }
            else if (customer.LoyaltyPoints >= 200)
            {
                customer.MembershipTier = "Bạc";
            }
            else if (customer.LoyaltyPoints >= 100)
            {
                customer.MembershipTier = "Đồng";
            }
            else
            {
                customer.MembershipTier = "Thành viên";
            }
            
            context.Entry(customer).State = EntityState.Modified;
            await context.SaveChangesAsync();
        }
    }

    public static async Task RecalculateAllCustomersPointsAndTiersAsync(PetStoreManagementContext context)
    {
        var customers = await context.Customers
            .Include(c => c.Orders)
            .ToListAsync();
            
        foreach (var customer in customers)
        {
            // 1. Chỉ tính điểm cho đơn hàng Đã hoàn thành (10 điểm/đơn)
            var completedOrdersCount = customer.Orders.Count(o => 
                OrderStatusHelper.ResolveStatusKey(o.Status) == "completed"
            );
            
            var pointsEarned = completedOrdersCount * 10;
            
            // 2. Điểm đã dùng (trừ điểm khi đặt đơn hàng chưa bị hủy/từ chối)
            var pointsRedeemed = customer.Orders
                .Where(o => {
                    var statusKey = OrderStatusHelper.ResolveStatusKey(o.Status);
                    return statusKey != "cancelled" && statusKey != "rejected";
                })
                .Sum(o => o.PointsRedeemed);
                
            customer.LoyaltyPoints = Math.Max(0, pointsEarned - pointsRedeemed);
            
            // 3. Phân hạng thành viên theo bậc điểm
            if (customer.LoyaltyPoints >= 400)
            {
                customer.MembershipTier = "VIP";
            }
            else if (customer.LoyaltyPoints >= 300)
            {
                customer.MembershipTier = "Vàng";
            }
            else if (customer.LoyaltyPoints >= 200)
            {
                customer.MembershipTier = "Bạc";
            }
            else if (customer.LoyaltyPoints >= 100)
            {
                customer.MembershipTier = "Đồng";
            }
            else
            {
                customer.MembershipTier = "Thành viên";
            }
            
            context.Entry(customer).State = EntityState.Modified;
        }
        await context.SaveChangesAsync();
    }
}
