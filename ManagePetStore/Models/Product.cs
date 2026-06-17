using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ManagePetStore.Models;

public partial class Product
{
    [Required(ErrorMessage = "Mã sản phẩm (SKU) không được để trống")]
    public string Sku { get; set; } = null!;

    [Required(ErrorMessage = "Tên sản phẩm không được để trống")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Đơn vị tính không được để trống")]
    public string Unit { get; set; } = null!;

    [Required(ErrorMessage = "Số lượng tồn không được để trống")]
    [Range(0, int.MaxValue, ErrorMessage = "Số lượng tồn phải lớn hơn hoặc bằng 0")]
    public int Stock { get; set; }

    [Required(ErrorMessage = "Tồn kho tối thiểu không được để trống")]
    [Range(0, int.MaxValue, ErrorMessage = "Tồn kho tối thiểu phải lớn hơn hoặc bằng 0")]
    public int MinStock { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public string? ShelfLocation { get; set; }

    [Required(ErrorMessage = "Giá bán không được để trống")]
    [Range(0, double.MaxValue, ErrorMessage = "Giá bán phải lớn hơn hoặc bằng 0")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "Giá nhập không được để trống")]
    [Range(0, double.MaxValue, ErrorMessage = "Giá nhập phải lớn hơn hoặc bằng 0")]
    public decimal CostPrice { get; set; }

    public string? ImageUrl { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn danh mục")]
    public int? CategoryId { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ProductCategory? Category { get; set; }

    public virtual ICollection<InventoryBatch> InventoryBatches { get; set; } = new List<InventoryBatch>();

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<StockMovementDetail> StockMovementDetails { get; set; } = new List<StockMovementDetail>();
}
