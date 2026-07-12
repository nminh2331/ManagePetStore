using ManagePetStore.Exceptions;
using ManagePetStore.Models;
using ManagePetStore.Repositories;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ManagePetStore.Services;

public class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _supplierRepo;

    public SupplierService(ISupplierRepository supplierRepo)
    {
        _supplierRepo = supplierRepo;
    }

    public async Task<IEnumerable<Supplier>> GetAllSuppliersAsync()
    {
        return await _supplierRepo.GetAllSuppliersAsync();
    }

    public async Task<Supplier?> GetSupplierByIdAsync(int id)
    {
        return await _supplierRepo.GetSupplierByIdAsync(id);
    }

    public async Task<IEnumerable<Supplier>> GetSuppliersByCategoryAsync(int categoryId)
    {
        return await _supplierRepo.GetSuppliersByCategoryAsync(categoryId);
    }

    public async Task AddSupplierAsync(Supplier supplier, List<int> categoryIds)
    {
        await _supplierRepo.AddSupplierAsync(supplier);
        await _supplierRepo.AssignCategoriesToSupplierAsync(supplier.SupplierId, categoryIds);
    }

    public async Task UpdateSupplierAsync(Supplier supplier, List<int> categoryIds)
    {
        await _supplierRepo.UpdateSupplierAsync(supplier);
        await _supplierRepo.AssignCategoriesToSupplierAsync(supplier.SupplierId, categoryIds);
    }

    public async Task DeleteSupplierAsync(int id)
    {
        // Could add validation to check if supplier has existing stock movements before deleting
        await _supplierRepo.DeleteSupplierAsync(id);
    }

    public async Task UpdateSupplierProductsAsync(int supplierId, List<string> productSkus)
    {
        await _supplierRepo.AssignProductsToSupplierAsync(supplierId, productSkus ?? new List<string>());
    }

    public async Task<List<string>> GetSupplierProductSkusAsync(int supplierId)
    {
        return await _supplierRepo.GetSupplierProductSkusAsync(supplierId);
    }

    public async Task<IEnumerable<Supplier>> GetSuppliersByProductSkuAsync(string sku)
    {
        return await _supplierRepo.GetSuppliersByProductSkuAsync(sku);
    }
}
