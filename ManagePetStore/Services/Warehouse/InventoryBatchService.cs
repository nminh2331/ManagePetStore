/**
 * Project: Pet Store Management System (PSMS)
 * File: InventoryBatchService.cs
 * Author: Tran Duong
 * Date: June 10, 2026
 * Last Update: July 17, 2026
 * Description: Triá»ƒn khai dá»‹ch vá»¥ quáº£n lÃ½ lÃ´ hÃ ng vÃ  Ä‘á»“ng bá»™ sá»‘ lÆ°á»£ng tá»“n kho vá»›i Sáº£n pháº©m.
 */
using ManagePetStore.Repositories.Warehouse;
using ManagePetStore.Exceptions;
using ManagePetStore.Models;
using ManagePetStore.Repositories;

namespace ManagePetStore.Services.Warehouse;

public class InventoryBatchService : IInventoryBatchService
{
    private readonly IInventoryBatchRepository _batchRepo;
    private readonly IProductRepository _productRepo;

    public InventoryBatchService(
        IInventoryBatchRepository batchRepo,
        IProductRepository productRepo)
    {
        _batchRepo = batchRepo;
        _productRepo = productRepo;
    }

    public async Task<IEnumerable<InventoryBatch>> GetBatchesByProductSku(string productSku)
    {
        return await _batchRepo.GetBatchesByProductSku(productSku);
    }

    public async Task<InventoryBatch?> GetBatchById(int batchId)
    {
        return await _batchRepo.GetBatchById(batchId);
    }

    public async Task CreateBatch(InventoryBatch batch)
    {
        var product = await _productRepo.GetProductBySku(batch.ProductSku);
        if (product == null)
            throw new ServiceException($"KhÃ´ng tÃ¬m tháº¥y sáº£n pháº©m mÃ£ '{batch.ProductSku}'.");

        batch.ReceivedDate = DateTime.Now;
        if (batch.Quantity < 0)
            throw new ServiceException("Sá»‘ lÆ°á»£ng nháº­p khÃ´ng Ä‘Æ°á»£c Ã¢m.");
        
        batch.CurrentQuantity = batch.Quantity; // Khi má»›i nháº­p, sá»‘ lÆ°á»£ng hiá»‡n táº¡i báº±ng sá»‘ lÆ°á»£ng nháº­p

        await _batchRepo.AddBatch(batch);

        // Äá»“ng bá»™ Stock cá»§a Product
        product.Stock += batch.CurrentQuantity;
        await _productRepo.UpdateProduct(product);
    }

    public async Task UpdateBatch(int batchId, int newQuantity, DateTime newExpiryDate)
    {
        var batch = await _batchRepo.GetBatchById(batchId);
        if (batch == null)
            throw new ServiceException("KhÃ´ng tÃ¬m tháº¥y lÃ´ hÃ ng.");

        var product = await _productRepo.GetProductBySku(batch.ProductSku);
        if (product == null)
            throw new ServiceException("Sáº£n pháº©m cá»§a lÃ´ hÃ ng khÃ´ng tá»“n táº¡i.");

        if (newQuantity < 0)
            throw new ServiceException("Sá»‘ lÆ°á»£ng khÃ´ng Ä‘Æ°á»£c Ã¢m.");

        // TÃ­nh chÃªnh lá»‡ch Ä‘á»ƒ cáº­p nháº­t vÃ o Product
        int diff = newQuantity - batch.CurrentQuantity;

        batch.CurrentQuantity = newQuantity;
        batch.ExpiryDate = newExpiryDate;

        await _batchRepo.UpdateBatch(batch);

        // Äá»“ng bá»™ Stock cá»§a Product
        product.Stock += diff;
        if (product.Stock < 0) product.Stock = 0; // Äáº£m báº£o an toÃ n
        await _productRepo.UpdateProduct(product);
    }

    public async Task DeleteBatch(int batchId)
    {
        var batch = await _batchRepo.GetBatchById(batchId);
        if (batch != null)
        {
            var product = await _productRepo.GetProductBySku(batch.ProductSku);
            if (product != null)
            {
                product.Stock -= batch.CurrentQuantity;
                if (product.Stock < 0) product.Stock = 0;
                await _productRepo.UpdateProduct(product);
            }

            await _batchRepo.DeleteBatch(batchId);
        }
    }

    public async Task DeductStockFIFO(string productSku, int quantityToDeduct)
    {
        var product = await _productRepo.GetProductBySku(productSku);
        if (product == null) throw new ServiceException("Sáº£n pháº©m khÃ´ng tá»“n táº¡i.");

        if (product.Stock < quantityToDeduct)
            throw new ServiceException($"Sá»‘ lÆ°á»£ng tá»“n kho khÃ´ng Ä‘á»§ Ä‘á»ƒ xuáº¥t ({product.Stock} < {quantityToDeduct}).");

        var batches = (await _batchRepo.GetBatchesByProductSku(productSku))
            .Where(b => b.CurrentQuantity > 0)
            .OrderBy(b => b.ReceivedDate) // CÅ© nháº¥t xuáº¥t trÆ°á»›c
            .ToList();

        int remainingToDeduct = quantityToDeduct;

        foreach (var batch in batches)
        {
            if (remainingToDeduct <= 0) break;

            if (batch.CurrentQuantity <= remainingToDeduct)
            {
                // Trá»« sáº¡ch lÃ´ nÃ y
                remainingToDeduct -= batch.CurrentQuantity;
                batch.CurrentQuantity = 0;
            }
            else
            {
                // Trá»« má»™t pháº§n lÃ´ nÃ y
                batch.CurrentQuantity -= remainingToDeduct;
                remainingToDeduct = 0;
            }
            await _batchRepo.UpdateBatch(batch);
        }

        // Cáº­p nháº­t tá»•ng tá»“n kho
        product.Stock -= quantityToDeduct;
        await _productRepo.UpdateProduct(product);
    }

    public async Task RestockToBatches(string productSku, int quantityToRestock)
    {
        var product = await _productRepo.GetProductBySku(productSku);
        if (product == null) throw new ServiceException("Sáº£n pháº©m khÃ´ng tá»“n táº¡i.");

        if (quantityToRestock <= 0) return;

        // TÃ¬m lÃ´ hÃ ng nháº­p vÃ o gáº§n nháº¥t (hoáº·c lÃ´ cÃ³ háº¡n sá»­ dá»¥ng xa nháº¥t)
        var newestBatch = (await _batchRepo.GetBatchesByProductSku(productSku))
            .OrderByDescending(b => b.ReceivedDate)
            .FirstOrDefault();

        if (newestBatch != null)
        {
            // Cá»™ng tráº£ láº¡i vÃ o lÃ´ má»›i nháº¥t
            newestBatch.CurrentQuantity += quantityToRestock;
            await _batchRepo.UpdateBatch(newestBatch);
        }
        else
        {
            // Náº¿u khÃ´ng cÃ³ báº¥t ká»³ lÃ´ nÃ o (hiáº¿m), ta cÃ³ thá»ƒ táº¡o 1 lÃ´ tá»± Ä‘á»™ng
            // NhÆ°ng hiá»‡n táº¡i theo flow cÅ©, chá»‰ cáº§n bá» qua vÃ  cá»™ng vÃ o Product.Stock
        }

        // Cáº­p nháº­t tá»•ng tá»“n kho
        product.Stock += quantityToRestock;
        await _productRepo.UpdateProduct(product);
    }
}
