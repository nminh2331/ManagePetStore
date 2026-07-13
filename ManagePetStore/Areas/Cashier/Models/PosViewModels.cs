using System;
using System.Collections.Generic;

namespace ManagePetStore.Areas.Cashier.Models
{
    public class PosSubmitOrderDto
    {
        public int CustomerId { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = "Tiền mặt";
        public int PointsUsed { get; set; }
        public decimal CashAmount { get; set; }
        public decimal OnlineAmount { get; set; }
        public string? VoucherCode { get; set; }
        public decimal VoucherDiscount { get; set; }
        public List<PosCartItemDto> Items { get; set; } = new List<PosCartItemDto>();
        public bool IsAtCounter { get; set; }
    }

    public class PosCartItemDto
    {
        public string Type { get; set; } = ""; // "Product" or "Spa"
        public string Id { get; set; } = ""; // SKU for Product, ID string for Spa
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Total { get; set; }
        public int? PetId { get; set; }
        public decimal? PetWeight { get; set; }
        public int? GroomerId { get; set; }
        public DateTime? AppointmentTime { get; set; }
        public int? BookingId { get; set; }
        public int? HotelCheckoutId { get; set; }
    }

    public class PosQuickRegisterDto
    {
        public string CustomerName { get; set; } = "";
        public string Phone { get; set; } = "";
        public string PetName { get; set; } = "";
        public string PetType { get; set; } = "Chó";
    }
}
