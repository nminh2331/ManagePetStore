using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ManagePetStore.Models;
using ManagePetStore.Areas.Customer.Models;

namespace ManagePetStore.Services.Customer;

public static class CustomerRewardHelper
{
    /// <summary>
    /// RÀNG BUỘC ĐỔI ĐIỂM THEO HẠNG THÀNH VIÊN (LOYALTY POINT EXCHANGE RATE BY TIER)
    /// - Hạng VIP (>= 400 điểm): 1 điểm = 3,000 VNĐ
    /// - Hạng Vàng (300 - 399 điểm): 1 điểm = 1,500 VNĐ
    /// - Hạng Bạc (200 - 299 điểm): 1 điểm = 1,000 VNĐ
    /// - Hạng Đồng (100 - 199 điểm): 1 điểm = 700 VNĐ
    /// - Hạng Thành viên (< 100 điểm): 1 điểm = 500 VNĐ
    /// </summary>
    public static decimal GetPointRateByTier(string? tier)
    {
        if (string.IsNullOrWhiteSpace(tier)) return 500m;
        var normalized = tier.Trim().ToLowerInvariant();
        return normalized switch
        {
            "vip" => 3000m,
            "vàng" or "gold" => 1500m,
            "bạc" or "silver" => 1000m,
            "đồng" or "bronze" => 700m,
            _ => 500m
        };
    }

    /// <summary>
    /// LUỒNG TÍNH LẠI ĐIỂM VÀ TỰ ĐỘNG CẬP NHẬT/HẠ RANK THÀNH VIÊN
    /// - 1. Điểm tích lũy = Tổng số đơn hoàn thành (completed) * 10 điểm.
    /// - 2. Điểm khả dụng (LoyaltyPoints) = Max(0, điểm_tích_lũy - tổng_điểm_đã_dùng).
    /// - 3. Cập nhật Hạng thành viên (MembershipTier) dựa trên số điểm còn lại:
    ///   + >= 400 pts -> VIP
    ///   + 300 - 399 pts -> Vàng (Nếu dùng điểm bị giảm từ 400 xuống 300 sẽ tự động hạ rank từ VIP xuống Vàng)
    ///   + 200 - 299 pts -> Bạc
    ///   + 100 - 199 pts -> Đồng
    ///   + < 100 pts -> Thành viên
    /// </summary>
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
                
            // Số điểm còn lại thực tế của khách hàng
            customer.LoyaltyPoints = Math.Max(0, pointsEarned - pointsRedeemed);
            
            // 3. Phân hạng thành viên động (Tự động nâng/hạ rank theo số điểm còn lại)
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
