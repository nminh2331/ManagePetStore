using ManagePetStore.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ManagePetStore.Services;

public interface ISupplierService
{
    Task<IEnumerable<Supplier>> GetAllSuppliersAsync();
    Task<Supplier?> GetSupplierByIdAsync(int id);
    Task<IEnumerable<Supplier>> GetSuppliersByCategoryAsync(int categoryId);
    Task AddSupplierAsync(Supplier supplier, List<int> categoryIds);
    Task UpdateSupplierAsync(Supplier supplier, List<int> categoryIds);
    Task DeleteSupplierAsync(int id);
    Task UpdateSupplierProductsAsync(int supplierId, List<string> productSkus);
    Task<List<string>> GetSupplierProductSkusAsync(int supplierId);
    Task<IEnumerable<Supplier>> GetSuppliersByProductSkuAsync(string sku);
}
