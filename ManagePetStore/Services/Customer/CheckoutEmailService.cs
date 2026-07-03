
// HÀ HOÀNG HIỆP CODE

using System.Text;
using ManagePetStore.Areas.Customer.Models;
using ManagePetStore.Services;

namespace ManagePetStore.Services.Customer;

public class CheckoutEmailService : ICheckoutEmailService
{
    private readonly IEmailService _emailService;

    public CheckoutEmailService(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task SendOrderConfirmationAsync(
        string toEmail,
        CheckoutSuccessViewModel order,
        IReadOnlyList<CartLineItemViewModel> items,
        string? orderNote)
    {
        var displayOrderId = FormatDisplayOrderId(order.OrderId);
        var subject = $"Xác nhận đơn hàng PetStore - {displayOrderId}";

        var itemsHtml = new StringBuilder();
        foreach (var item in items)
        {
            itemsHtml.Append($@"
                <tr>
                    <td style=""padding: 10px 0; border-bottom: 1px solid #f3e8dc;"">
                        <strong>{item.Name}</strong><br/>
                        <span style=""color: #6b7280; font-size: 13px;"">SKU: {item.Sku} · SL: {item.Quantity}</span>
                    </td>
                    <td style=""padding: 10px 0; border-bottom: 1px solid #f3e8dc; text-align: right; color: #f97316; font-weight: 700;"">
                        {item.LineTotal:N0}đ
                    </td>
                </tr>");
        }

        var noteHtml = string.IsNullOrWhiteSpace(orderNote)
            ? ""
            : $@"<p style=""margin: 16px 0 0; color: #6b7280;""><strong>Ghi chú:</strong> {orderNote}</p>";

        var body = $@"
            <div style=""font-family: Arial, sans-serif; max-width: 560px; margin: 0 auto;"">
                <h2 style=""color: #f97316;"">PetStore - Đặt hàng thành công</h2>
                <p>Xin chào <strong>{order.FullName}</strong>,</p>
                <p>Cảm ơn bạn đã đặt hàng tại PetStore. Đơn hàng <strong>{displayOrderId}</strong> đã được ghi nhận.</p>

                <div style=""background: #fff7ed; border-radius: 12px; padding: 16px; margin: 20px 0;"">
                    <p style=""margin: 0 0 8px;""><strong>Mã đơn:</strong> {order.OrderId}</p>
                    <p style=""margin: 0 0 8px;""><strong>Người nhận:</strong> {order.FullName}</p>
                    <p style=""margin: 0 0 8px;""><strong>SĐT:</strong> {order.Phone}</p>
                    <p style=""margin: 0 0 8px;""><strong>Địa chỉ:</strong> {order.ShippingAddress}</p>
                    <p style=""margin: 0 0 8px;""><strong>Thanh toán:</strong> {order.PaymentMethod}</p>
                    <p style=""margin: 0;""><strong>Tổng tiền:</strong> <span style=""color: #f97316; font-size: 18px;"">{order.Total:N0}đ</span></p>
                </div>

                <h3 style=""color: #3d2314; font-size: 16px;"">Chi tiết sản phẩm</h3>
                <table style=""width: 100%; border-collapse: collapse;"">
                    {itemsHtml}
                </table>
                {noteHtml}

                <p style=""color: #6b7280; font-size: 13px; margin-top: 24px;"">Nếu bạn không thực hiện đặt hàng này, vui lòng liên hệ PetStore ngay.</p>
            </div>";

        await _emailService.SendEmailAsync(toEmail, subject, body);
    }

    private static string FormatDisplayOrderId(string orderId)
    {
        var parts = orderId.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"#OD-{parts[^1]}" : $"#{orderId}";
    }
}
