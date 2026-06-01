using System.Collections.Generic;

namespace ManagePetStore.Models;

/// <summary>
/// ViewModel returned by IProductCategoryService.GetSummaryAsync().
/// Carries both the category list and pre-calculated stats
/// so the controller stays thin.
/// </summary>
public class CategorySummaryViewModel
{
    public IEnumerable<ProductCategory> Categories  { get; set; } = [];
    public int TotalCategories  { get; set; }
    public int TotalProducts    { get; set; }
    public int EmptyCategories  { get; set; }
}
