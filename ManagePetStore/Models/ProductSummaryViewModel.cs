using System.Collections.Generic;

namespace ManagePetStore.Models;

/// <summary>
/// ViewModel returned by IProductService.GetSummaryAsync().
/// Carries both the product list and pre-calculated warehouse stats
/// so the controller stays thin.
/// </summary>
public class ProductSummaryViewModel
{
    public IEnumerable<Product> Products { get; set; } = [];
    public int     TotalProducts    { get; set; }
    public int     LowStockCount    { get; set; }
    public int     OutOfStockCount  { get; set; }
    public decimal TotalValue       { get; set; }
    public int     CategoryCount    { get; set; }
}
