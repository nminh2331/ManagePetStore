using ManagePetStore.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ManagePetStore.Repositories;

public interface ISupplierRepository
{
    Task<IEnumerable<Supplier>> GetAllSuppliersAsync();
    Task<Supplier?> GetSupplierByIdAsync(int id);
    Task<IEnumerable<Supplier>> GetSuppliersByCategoryAsync(int categoryId);
    Task AddSupplierAsync(Supplier supplier);
    Task UpdateSupplierAsync(Supplier supplier);
    Task DeleteSupplierAsync(int id);
    
    // Category mapping
    Task AssignCategoriesToSupplierAsync(int supplierId, List<int> categoryIds);

    // Product-specific mapping (Phương án A)
    Task AssignProductsToSupplierAsync(int supplierId, List<string> productSkus);
    Task<List<string>> GetSupplierProductSkusAsync(int supplierId);
    Task<IEnumerable<Supplier>> GetSuppliersByProductSkuAsync(string sku);
}
